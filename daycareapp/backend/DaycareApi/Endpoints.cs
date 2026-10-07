using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace DaycareApi;

public static class Api
{
    const int EatHours = 3; // Ethiopia is UTC+3 all year; used to find "today" for attendance
    static readonly string[] EntryTypes = ["meal", "nap", "diaper", "activity", "mood", "note"];

    static string Hash(string s) => BCrypt.Net.BCrypt.HashPassword(s);
    static IResult Bad(string m) => Results.BadRequest(new { error = m });
    static IResult Conflict(string m) => Results.Conflict(new { error = m });

    // The children the signed-in user is allowed to see:
    // admin = whole center, staff = their classrooms, parent = their own children.
    static IQueryable<Child> Visible(AppDb db, ClaimsPrincipal p)
    {
        var uid = p.Uid();
        var q = db.Children.AsQueryable();
        return p.RoleName() switch
        {
            Roles.Staff => q.Where(c => c.IsActive &&
                db.StaffClassrooms.Any(s => s.UserId == uid && s.ClassroomId == c.ClassroomId)),
            Roles.Parent => q.Where(c => c.IsActive &&
                db.ChildGuardians.Any(g => g.ChildId == c.Id && g.Guardian.UserId == uid)),
            _ => q,
        };
    }

    static (DateTime start, DateTime end) Day(DateOnly d)
    {
        var s = DateTime.SpecifyKind(d.ToDateTime(TimeOnly.MinValue).AddHours(-EatHours), DateTimeKind.Utc);
        return (s, s.AddDays(1));
    }

    public static void MapApi(this WebApplication app)
    {
        var api = app.MapGroup("/api");
        var any = api.MapGroup("").RequireAuthorization();
        var ops = api.MapGroup("").RequireAuthorization(p => p.RequireRole(Roles.Admin, Roles.Staff));
        var adm = api.MapGroup("").RequireAuthorization(p => p.RequireRole(Roles.Admin));
        var sup = api.MapGroup("/centers").RequireAuthorization(p => p.RequireRole(Roles.SuperAdmin));

        // ---------- auth ----------
        api.MapPost("/auth/login", async (LoginReq r, AppDb db, TokenService ts) =>
        {
            var email = (r.Email ?? "").Trim().ToLower();
            var u = await db.Users.FirstOrDefaultAsync(x => x.Email == email);
            if (u is null || !u.IsActive || !BCrypt.Net.BCrypt.Verify(r.Password ?? "", u.PasswordHash))
                return Results.Unauthorized();
            if (u.CenterId is Guid cid && !await db.Centers.AnyAsync(c => c.Id == cid && c.IsActive))
                return Results.Json(new { error = "This daycare account is disabled" }, statusCode: 403);
            return Results.Ok(new { token = ts.Create(u), user = new { u.Id, u.FullName, u.Role, u.CenterId } });
        });

        // ---------- super admin: daycares ----------
        sup.MapGet("", async (AppDb db) =>
        {
            var counts = await db.Children.IgnoreQueryFilters().GroupBy(c => c.CenterId)
                .Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
            var centers = await db.Centers.OrderBy(c => c.Name).ToListAsync();
            return centers.Select(c => new { c.Id, c.Name, c.Address, c.Phone, c.IsActive, Children = counts.GetValueOrDefault(c.Id) });
        });

        sup.MapPost("", async (NewCenter r, AppDb db) =>
        {
            if (string.IsNullOrWhiteSpace(r.Name) || string.IsNullOrWhiteSpace(r.AdminName) || string.IsNullOrWhiteSpace(r.AdminEmail))
                return Bad("Daycare name, admin name and admin email are required");
            if ((r.AdminPassword ?? "").Length < 6) return Bad("Password must be at least 6 characters");
            var email = r.AdminEmail.Trim().ToLower();
            if (await db.Users.AnyAsync(u => u.Email == email)) return Conflict("Email is already used");
            var c = new Center { Name = r.Name.Trim(), Address = r.Address, Phone = r.Phone };
            db.Centers.Add(c);
            db.Users.Add(new User { CenterId = c.Id, Email = email, PasswordHash = Hash(r.AdminPassword!), Role = Roles.Admin, FullName = r.AdminName.Trim() });
            await db.SaveChangesAsync();
            return Results.Ok(new { c.Id });
        });

        sup.MapPut("/{id:guid}/active", async (Guid id, ActiveReq r, AppDb db) =>
        {
            var c = await db.Centers.FindAsync(id);
            if (c is null) return Results.NotFound();
            c.IsActive = r.IsActive;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ---------- admin: staff ----------
        adm.MapGet("/staff", async (AppDb db, ClaimsPrincipal p) =>
        {
            var cid = p.Cid();
            return await db.Users.Where(u => u.CenterId == cid && u.Role == Roles.Staff).OrderBy(u => u.FullName)
                .Select(u => new
                {
                    u.Id, u.FullName, u.Email, u.Phone, u.IsActive,
                    JobTitle = db.StaffProfiles.Where(s => s.UserId == u.Id).Select(s => s.JobTitle).FirstOrDefault(),
                    ClassroomIds = db.StaffClassrooms.Where(s => s.UserId == u.Id).Select(s => s.ClassroomId).ToList(),
                }).ToListAsync();
        });

        adm.MapPost("/staff", async (NewStaff r, AppDb db, ClaimsPrincipal p) =>
        {
            if (string.IsNullOrWhiteSpace(r.FullName) || string.IsNullOrWhiteSpace(r.Email)) return Bad("Name and email are required");
            if ((r.Password ?? "").Length < 6) return Bad("Password must be at least 6 characters");
            var email = r.Email.Trim().ToLower();
            if (await db.Users.AnyAsync(u => u.Email == email)) return Conflict("Email is already used");
            var u = new User { CenterId = p.Cid(), Email = email, PasswordHash = Hash(r.Password!), Role = Roles.Staff, FullName = r.FullName.Trim(), Phone = r.Phone };
            db.Users.Add(u);
            db.StaffProfiles.Add(new StaffProfile { UserId = u.Id, JobTitle = r.JobTitle, HireDate = DateOnly.FromDateTime(DateTime.UtcNow) });
            var ids = r.ClassroomIds ?? new();
            var valid = await db.Classrooms.Where(c => ids.Contains(c.Id)).Select(c => c.Id).ToListAsync();
            db.StaffClassrooms.AddRange(valid.Select(c => new StaffClassroom { UserId = u.Id, ClassroomId = c }));
            await db.SaveChangesAsync();
            return Results.Ok(new { u.Id });
        });

        adm.MapPut("/staff/{id:guid}", async (Guid id, StaffUpdate r, AppDb db, ClaimsPrincipal p) =>
        {
            var cid = p.Cid();
            var u = await db.Users.FirstOrDefaultAsync(x => x.Id == id && x.CenterId == cid && x.Role == Roles.Staff);
            if (u is null) return Results.NotFound();
            u.IsActive = r.IsActive;
            var ids = r.ClassroomIds ?? new();
            var valid = await db.Classrooms.Where(c => ids.Contains(c.Id)).Select(c => c.Id).ToListAsync();
            var old = await db.StaffClassrooms.Where(s => s.UserId == id).ToListAsync();
            db.StaffClassrooms.RemoveRange(old.Where(o => !valid.Contains(o.ClassroomId)));
            db.StaffClassrooms.AddRange(valid.Where(v => old.All(o => o.ClassroomId != v))
                .Select(v => new StaffClassroom { UserId = id, ClassroomId = v }));
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ---------- classrooms ----------
        ops.MapGet("/classrooms", async (AppDb db, ClaimsPrincipal p) =>
        {
            var q = db.Classrooms.AsQueryable();
            if (p.RoleName() == Roles.Staff)
            {
                var uid = p.Uid();
                q = q.Where(c => db.StaffClassrooms.Any(s => s.UserId == uid && s.ClassroomId == c.Id));
            }
            return await q.OrderBy(c => c.Name).Select(c => new { c.Id, c.Name, c.AgeGroup, c.Capacity }).ToListAsync();
        });

        adm.MapPost("/classrooms", async (ClassroomReq r, AppDb db, ClaimsPrincipal p) =>
        {
            if (string.IsNullOrWhiteSpace(r.Name)) return Bad("Classroom name is required");
            var c = new Classroom { CenterId = p.Cid()!.Value, Name = r.Name.Trim(), AgeGroup = r.AgeGroup, Capacity = r.Capacity };
            db.Classrooms.Add(c);
            await db.SaveChangesAsync();
            return Results.Ok(new { c.Id });
        });

        // ---------- children + guardians ----------
        any.MapGet("/children", async (AppDb db, ClaimsPrincipal p) =>
        {
            var q = Visible(db, p).OrderBy(c => c.FirstName);
            if (p.RoleName() == Roles.Parent)
                return Results.Ok(await q.Select(c => new
                {
                    c.Id, c.FirstName, c.LastName, c.DateOfBirth, c.ClassroomId, Classroom = c.Classroom.Name,
                    c.Allergies, c.MedicalNotes, c.IsActive,
                }).ToListAsync());
            return Results.Ok(await q.Select(c => new
            {
                c.Id, c.FirstName, c.LastName, c.DateOfBirth, c.ClassroomId, Classroom = c.Classroom.Name,
                c.Allergies, c.MedicalNotes, c.IsActive,
                Guardians = db.ChildGuardians.Where(g => g.ChildId == c.Id)
                    .Select(g => new { g.GuardianId, g.Guardian.FullName, g.Guardian.Phone, g.Relationship, g.IsPrimary, g.CanPickup }).ToList(),
            }).ToListAsync());
        });

        adm.MapPost("/children", async (ChildReq r, AppDb db, ClaimsPrincipal p) =>
        {
            if (string.IsNullOrWhiteSpace(r.FirstName) || string.IsNullOrWhiteSpace(r.LastName)) return Bad("First and last name are required");
            if (!await db.Classrooms.AnyAsync(c => c.Id == r.ClassroomId)) return Bad("Choose a classroom");
            var c = new Child
            {
                CenterId = p.Cid()!.Value, ClassroomId = r.ClassroomId, FirstName = r.FirstName.Trim(),
                LastName = r.LastName.Trim(), DateOfBirth = r.DateOfBirth, Allergies = r.Allergies, MedicalNotes = r.MedicalNotes,
            };
            db.Children.Add(c);
            await db.SaveChangesAsync();
            return Results.Ok(new { c.Id });
        });

        adm.MapPut("/children/{id:guid}", async (Guid id, ChildReq r, AppDb db) =>
        {
            var c = await db.Children.FirstOrDefaultAsync(x => x.Id == id);
            if (c is null) return Results.NotFound();
            if (!await db.Classrooms.AnyAsync(x => x.Id == r.ClassroomId)) return Bad("Choose a classroom");
            c.FirstName = r.FirstName.Trim(); c.LastName = r.LastName.Trim(); c.DateOfBirth = r.DateOfBirth;
            c.ClassroomId = r.ClassroomId; c.Allergies = r.Allergies; c.MedicalNotes = r.MedicalNotes;
            c.IsActive = r.IsActive ?? c.IsActive;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        adm.MapGet("/guardians", async (AppDb db) =>
            await db.Guardians.OrderBy(g => g.FullName)
                .Select(g => new { g.Id, g.FullName, g.Phone, g.Email, HasAccount = g.UserId != null }).ToListAsync());

        // create a guardian; optional password = also create a parent login; optional childId = link right away
        adm.MapPost("/guardians", async (GuardianReq r, AppDb db, ClaimsPrincipal p) =>
        {
            if (string.IsNullOrWhiteSpace(r.FullName)) return Bad("Guardian name is required");
            var cid = p.Cid()!.Value;
            var g = new Guardian { CenterId = cid, FullName = r.FullName.Trim(), Phone = r.Phone, Email = r.Email?.Trim().ToLower() };
            if (!string.IsNullOrWhiteSpace(r.Password))
            {
                if (string.IsNullOrWhiteSpace(r.Email)) return Bad("Email is required to create a login");
                if (r.Password.Length < 6) return Bad("Password must be at least 6 characters");
                var email = r.Email.Trim().ToLower();
                if (await db.Users.AnyAsync(u => u.Email == email)) return Conflict("Email is already used");
                var u = new User { CenterId = cid, Email = email, PasswordHash = Hash(r.Password), Role = Roles.Parent, FullName = g.FullName, Phone = r.Phone };
                db.Users.Add(u);
                g.UserId = u.Id;
            }
            db.Guardians.Add(g);
            if (r.ChildId is Guid childId && await db.Children.AnyAsync(c => c.Id == childId))
                db.ChildGuardians.Add(new ChildGuardian
                {
                    ChildId = childId, Guardian = g, Relationship = r.Relationship ?? "Parent",
                    IsPrimary = r.IsPrimary, CanPickup = r.CanPickup,
                });
            await db.SaveChangesAsync();
            return Results.Ok(new { g.Id });
        });

        adm.MapPost("/children/{id:guid}/guardians", async (Guid id, LinkReq r, AppDb db) =>
        {
            if (!await db.Children.AnyAsync(c => c.Id == id) || !await db.Guardians.AnyAsync(g => g.Id == r.GuardianId))
                return Results.NotFound();
            if (await db.ChildGuardians.AnyAsync(x => x.ChildId == id && x.GuardianId == r.GuardianId))
                return Conflict("Already linked to this child");
            db.ChildGuardians.Add(new ChildGuardian
            {
                ChildId = id, GuardianId = r.GuardianId, Relationship = r.Relationship ?? "Parent",
                IsPrimary = r.IsPrimary, CanPickup = r.CanPickup,
            });
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        adm.MapDelete("/children/{id:guid}/guardians/{gid:guid}", async (Guid id, Guid gid, AppDb db) =>
        {
            var x = await db.ChildGuardians.FirstOrDefaultAsync(l => l.ChildId == id && l.GuardianId == gid);
            if (x is null) return Results.NotFound();
            db.ChildGuardians.Remove(x);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ---------- attendance ----------
        ops.MapPost("/attendance/check-in", async (CheckInReq r, AppDb db, ClaimsPrincipal p) =>
        {
            if (!await Visible(db, p).AnyAsync(c => c.Id == r.ChildId && c.IsActive)) return Results.NotFound();
            if (await db.Attendance.AnyAsync(a => a.ChildId == r.ChildId && a.CheckOutAt == null))
                return Conflict("Child is already checked in");
            db.Attendance.Add(new Attendance { CenterId = p.Cid()!.Value, ChildId = r.ChildId, CheckedInBy = p.Uid() });
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        ops.MapPost("/attendance/{id:guid}/check-out", async (Guid id, CheckOutReq r, AppDb db, ClaimsPrincipal p) =>
        {
            var ids = Visible(db, p).Select(c => c.Id);
            var a = await db.Attendance.FirstOrDefaultAsync(x => x.Id == id && x.CheckOutAt == null && ids.Contains(x.ChildId));
            if (a is null) return Results.NotFound();
            a.CheckOutAt = DateTime.UtcNow;
            a.CheckedOutBy = p.Uid();
            if (r.PickedUpByGuardianId is Guid g &&
                await db.ChildGuardians.AnyAsync(x => x.ChildId == a.ChildId && x.GuardianId == g && x.CanPickup))
                a.PickedUpByGuardianId = g;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ?date=YYYY-MM-DD -> that day (plus any visit still open); no date -> last 30 days
        any.MapGet("/attendance", async (AppDb db, ClaimsPrincipal p, DateOnly? date, Guid? childId) =>
        {
            var ids = Visible(db, p).Select(c => c.Id);
            var q = db.Attendance.Where(a => ids.Contains(a.ChildId));
            if (childId != null) q = q.Where(a => a.ChildId == childId);
            if (date is DateOnly d)
            {
                var (start, end) = Day(d);
                q = q.Where(a => (a.CheckInAt >= start && a.CheckInAt < end) || a.CheckOutAt == null);
            }
            else
            {
                var since = DateTime.UtcNow.AddDays(-30);
                q = q.Where(a => a.CheckInAt >= since);
            }
            return await q.OrderByDescending(a => a.CheckInAt).Take(300).Select(a => new
            {
                a.Id, a.ChildId, Child = a.Child.FirstName + " " + a.Child.LastName, a.CheckInAt, a.CheckOutAt,
                PickedUpBy = a.PickedUpByGuardian != null ? a.PickedUpByGuardian.FullName : null,
            }).ToListAsync();
        });

        // ---------- daily reports ----------
        ops.MapPost("/reports/entries", async (NewEntry r, AppDb db, ClaimsPrincipal p) =>
        {
            if (!EntryTypes.Contains(r.EntryType)) return Bad("Invalid entry type");
            if (!await Visible(db, p).AnyAsync(c => c.Id == r.ChildId)) return Results.NotFound();
            var rep = await db.DailyReports.FirstOrDefaultAsync(x => x.ChildId == r.ChildId && x.ReportDate == r.Date);
            if (rep is null)
            {
                rep = new DailyReport { CenterId = p.Cid()!.Value, ChildId = r.ChildId, ReportDate = r.Date, CreatedBy = p.Uid() };
                db.DailyReports.Add(rep);
            }
            db.Entries.Add(new DailyReportEntry
            {
                Report = rep, EntryType = r.EntryType, Details = r.Details,
                OccurredAt = r.OccurredAt ?? DateTime.UtcNow, CreatedBy = p.Uid(),
            });
            await db.SaveChangesAsync();
            return Results.Ok(new { rep.Id });
        });

        ops.MapDelete("/reports/entries/{id:guid}", async (Guid id, AppDb db, ClaimsPrincipal p) =>
        {
            var ids = Visible(db, p).Select(c => c.Id);
            var e = await db.Entries.FirstOrDefaultAsync(x => x.Id == id && ids.Contains(x.Report.ChildId));
            if (e is null) return Results.NotFound();
            db.Entries.Remove(e);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // save the summary note; publish = true makes the report visible to parents
        ops.MapPut("/reports/{id:guid}", async (Guid id, ReportReq r, AppDb db, ClaimsPrincipal p) =>
        {
            var ids = Visible(db, p).Select(c => c.Id);
            var rep = await db.DailyReports.FirstOrDefaultAsync(x => x.Id == id && ids.Contains(x.ChildId));
            if (rep is null) return Results.NotFound();
            rep.GeneralNote = r.GeneralNote;
            rep.PublishedAt = r.Publish ? (rep.PublishedAt ?? DateTime.UtcNow) : null;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        any.MapGet("/reports", async (AppDb db, ClaimsPrincipal p, Guid? childId, DateOnly? date) =>
        {
            var ids = Visible(db, p).Select(c => c.Id);
            var q = db.DailyReports.Where(r => ids.Contains(r.ChildId));
            if (p.RoleName() == Roles.Parent) q = q.Where(r => r.PublishedAt != null);
            if (childId != null) q = q.Where(r => r.ChildId == childId);
            if (date is DateOnly d) q = q.Where(r => r.ReportDate == d);
            return await q.OrderByDescending(r => r.ReportDate).Take(60).Select(r => new
            {
                r.Id, r.ChildId, Child = r.Child.FirstName + " " + r.Child.LastName, r.ReportDate, r.GeneralNote, r.PublishedAt,
                Entries = r.Entries.OrderBy(e => e.OccurredAt)
                    .Select(e => new { e.Id, e.EntryType, e.OccurredAt, e.Details }).ToList(),
            }).ToListAsync();
        });

        // ---------- announcements ----------
        any.MapGet("/announcements", async (AppDb db, ClaimsPrincipal p) =>
        {
            var uid = p.Uid();
            var role = p.RoleName();
            var q = db.Announcements.AsQueryable();
            if (role == Roles.Staff)
                q = q.Where(a => a.ClassroomId == null ||
                    db.StaffClassrooms.Any(s => s.UserId == uid && s.ClassroomId == a.ClassroomId));
            else if (role == Roles.Parent)
            {
                var rooms = Visible(db, p).Select(c => c.ClassroomId);
                q = q.Where(a => a.ClassroomId == null || rooms.Contains(a.ClassroomId.Value));
            }
            return await q.OrderByDescending(a => a.PublishedAt).Take(50).Select(a => new
            {
                a.Id, a.Title, a.Body, a.PublishedAt,
                Classroom = a.Classroom != null ? a.Classroom.Name : null,
                Author = a.Author.FullName,
            }).ToListAsync();
        });

        // admin: whole center or any classroom. staff: only their own classrooms.
        ops.MapPost("/announcements", async (AnnReq r, AppDb db, ClaimsPrincipal p) =>
        {
            if (string.IsNullOrWhiteSpace(r.Title) || string.IsNullOrWhiteSpace(r.Body)) return Bad("Title and message are required");
            var isStaff = p.RoleName() == Roles.Staff;
            if (r.ClassroomId is Guid room)
            {
                if (!await db.Classrooms.AnyAsync(c => c.Id == room)) return Results.NotFound();
                var uid = p.Uid();
                if (isStaff && !await db.StaffClassrooms.AnyAsync(s => s.UserId == uid && s.ClassroomId == room))
                    return Results.StatusCode(403);
            }
            else if (isStaff) return Results.StatusCode(403);

            db.Announcements.Add(new Announcement
            {
                CenterId = p.Cid()!.Value, AuthorId = p.Uid(), ClassroomId = r.ClassroomId,
                Title = r.Title.Trim(), Body = r.Body.Trim(),
            });
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}

// ---------- request bodies ----------
public record LoginReq(string? Email, string? Password);
public record NewCenter(string Name, string? Address, string? Phone, string AdminName, string AdminEmail, string? AdminPassword);
public record ActiveReq(bool IsActive);
public record NewStaff(string FullName, string Email, string? Password, string? Phone, string? JobTitle, List<Guid>? ClassroomIds);
public record StaffUpdate(bool IsActive, List<Guid>? ClassroomIds);
public record ClassroomReq(string Name, string? AgeGroup, int Capacity);
public record ChildReq(string FirstName, string LastName, DateOnly DateOfBirth, Guid ClassroomId, string? Allergies, string? MedicalNotes, bool? IsActive);
public record GuardianReq(string FullName, string? Phone, string? Email, string? Password, Guid? ChildId, string? Relationship, bool IsPrimary, bool CanPickup = true);
public record LinkReq(Guid GuardianId, string? Relationship, bool IsPrimary, bool CanPickup = true);
public record CheckInReq(Guid ChildId);
public record CheckOutReq(Guid? PickedUpByGuardianId);
public record NewEntry(Guid ChildId, DateOnly Date, string EntryType, string? Details, DateTime? OccurredAt);
public record ReportReq(string? GeneralNote, bool Publish);
public record AnnReq(string Title, string Body, Guid? ClassroomId);
