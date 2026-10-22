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
        var staffOps = api.MapGroup("").RequireAuthorization(p => p.RequireRole(Roles.Staff));
        var adm = api.MapGroup("").RequireAuthorization(p => p.RequireRole(Roles.Admin));
        var sup = api.MapGroup("/centers").RequireAuthorization(p => p.RequireRole(Roles.SuperAdmin));

        // ---------- super admin: daycares ----------
        sup.MapGet("", async (AppDb db) =>
        {
            var counts = await db.Children.IgnoreQueryFilters().GroupBy(c => c.CenterId)
                .Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
            var centers = await db.Centers.OrderBy(c => c.Name).Select(c => new
            {
                c.Id,
                c.Name,
                c.Address,
                c.Phone,
                c.IsActive,
                Children = counts.GetValueOrDefault(c.Id),
                Admins = db.Users.Where(u => u.CenterId == c.Id && u.Role == Roles.Admin)
                    .OrderBy(u => u.FullName)
                    .Select(u => new { u.Id, u.FullName, u.Email, u.IsActive })
                    .ToList(),
            }).ToListAsync();
            return centers;
        });

        sup.MapGet("/admin-users", async (AppDb db) =>
            await db.Users.IgnoreQueryFilters().Where(u => u.Role == Roles.Admin)
                .OrderBy(u => u.FullName)
                .Select(u => new { u.Id, u.FullName, u.Email, u.CenterId, u.IsActive })
                .ToListAsync());

        sup.MapPut("/admin-users/{id:guid}", async (Guid id, AdminUserUpdate r, AppDb db) =>
        {
            var admin = await db.Users.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == id && u.Role == Roles.Admin);
            if (admin is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(r.FullName) || string.IsNullOrWhiteSpace(r.Email))
                return Bad("Admin name and email are required");
            var email = r.Email.Trim().ToLowerInvariant();
            if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Id != id && u.Email == email))
                return Conflict("Email is already used");
            if (r.CenterId is not Guid centerId || !await db.Centers.AnyAsync(c => c.Id == centerId))
                return Bad("Choose a valid daycare");

            admin.FullName = r.FullName.Trim();
            admin.Email = email;
            admin.Phone = r.Phone?.Trim();
            admin.CenterId = r.CenterId;
            admin.IsActive = r.IsActive;
            await db.SaveChangesAsync();
            return Results.NoContent();
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

        sup.MapPut("/{id:guid}", async (Guid id, CenterReq r, AppDb db) =>
        {
            var center = await db.Centers.FindAsync(id);
            if (center is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(r.Name)) return Bad("Daycare name is required");
            center.Name = r.Name.Trim();
            center.Address = r.Address?.Trim();
            center.Phone = r.Phone?.Trim();
            if (r.AdminId is Guid adminId)
            {
                var admin = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == adminId && u.Role == Roles.Admin);
                if (admin is null) return Bad("Choose a valid administrator");
                admin.CenterId = id;
            }
            await db.SaveChangesAsync();
            return Results.Ok(new { center.Id, center.Name, center.Address, center.Phone, center.IsActive });
        });

        sup.MapPut("/{id:guid}/active", async (Guid id, ActiveReq r, AppDb db) =>
        {
            var c = await db.Centers.FindAsync(id);
            if (c is null) return Results.NotFound();
            c.IsActive = r.IsActive;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Super admins manage the administrators of each centre.
        sup.MapGet("/{id:guid}/admins", async (Guid id, AppDb db) =>
            await db.Users.Where(u => u.CenterId == id && u.Role == Roles.Admin)
                .OrderBy(u => u.FullName)
                .Select(u => new { u.Id, u.FullName, u.Email, u.Phone, u.IsActive }).ToListAsync());

        sup.MapPut("/{id:guid}/admins/{adminId:guid}", async (Guid id, Guid adminId, AdminUpdate r, AppDb db) =>
        {
            var admin = await db.Users.FirstOrDefaultAsync(x => x.Id == adminId && x.CenterId == id && x.Role == Roles.Admin);
            if (admin is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(r.FullName) || string.IsNullOrWhiteSpace(r.Email))
                return Bad("Admin name and email are required");
            var email = r.Email.Trim().ToLowerInvariant();
            if (await db.Users.AnyAsync(x => x.Id != adminId && x.Email == email)) return Conflict("Email is already used");
            admin.FullName = r.FullName.Trim();
            admin.Email = email;
            admin.Phone = r.Phone?.Trim();
            admin.IsActive = r.IsActive;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ---------- admin: staff ----------
        adm.MapGet("/staff", async (AppDb db, ClaimsPrincipal p, string? search, int? page, int? pageSize) =>
        {
            var cid = p.Cid();
            var q = db.Users.Where(u => u.CenterId == cid && u.Role == Roles.Staff);
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(u => u.FullName.ToLower().Contains(search.ToLower()) || u.Email.ToLower().Contains(search.ToLower()));
            var total = await q.CountAsync();
            var paged = page is not null || pageSize is not null;
            var pno = Math.Max(page ?? 1, 1); var size = Math.Clamp(pageSize ?? 25, 1, 100);
            var rows = q.OrderBy(u => u.FullName);
            if (paged) rows = rows.Skip((pno - 1) * size).Take(size).OrderBy(u => u.FullName);
            var items = await rows
                .Select(u => new
                {
                    u.Id, u.FullName, u.Email, u.Phone, u.IsActive,
                    JobTitle = db.StaffProfiles.Where(s => s.UserId == u.Id).Select(s => s.JobTitle).FirstOrDefault(),
                    ClassroomIds = db.StaffClassrooms.Where(s => s.UserId == u.Id).Select(s => s.ClassroomId).ToList(),
                }).ToListAsync();
            return paged ? Results.Ok(new { items, total, page = pno, pageSize = size }) : Results.Ok(items);
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
            if (string.IsNullOrWhiteSpace(r.FullName) || string.IsNullOrWhiteSpace(r.Email))
                return Bad("Name and email are required");
            var email = r.Email.Trim().ToLowerInvariant();
            if (await db.Users.AnyAsync(x => x.Id != id && x.Email == email))
                return Conflict("Email is already used");
            u.FullName = r.FullName.Trim();
            u.Email = email;
            u.Phone = r.Phone?.Trim();
            u.IsActive = r.IsActive;
            if (!string.IsNullOrWhiteSpace(r.Password))
            {
                if (r.Password.Length < 6) return Bad("Password must be at least 6 characters");
                u.PasswordHash = Hash(r.Password);
            }
            var profile = await db.StaffProfiles.FirstOrDefaultAsync(s => s.UserId == id);
            if (profile is null)
            {
                profile = new StaffProfile { UserId = id };
                db.StaffProfiles.Add(profile);
            }
            profile.JobTitle = r.JobTitle?.Trim();
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
            return await q.OrderBy(c => c.Name).Select(c => new
            {
                c.Id,
                c.Name,
                c.AgeGroup,
                c.Capacity,
                StudentCount = db.Children.Count(child => child.ClassroomId == c.Id && child.IsActive),
            }).ToListAsync();
        });

        adm.MapPost("/classrooms", async (ClassroomReq r, AppDb db, ClaimsPrincipal p) =>
        {
            if (string.IsNullOrWhiteSpace(r.Name)) return Bad("Classroom name is required");
            var c = new Classroom { CenterId = p.Cid()!.Value, Name = r.Name.Trim(), AgeGroup = r.AgeGroup, Capacity = r.Capacity };
            db.Classrooms.Add(c);
            await db.SaveChangesAsync();
            return Results.Ok(new { c.Id });
        });

        adm.MapPut("/classrooms/{id:guid}", async (Guid id, ClassroomReq r, AppDb db, ClaimsPrincipal p) =>
        {
            var c = await db.Classrooms.FirstOrDefaultAsync(x => x.Id == id && x.CenterId == p.Cid());
            if (c is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(r.Name)) return Bad("Classroom name is required");
            c.Name = r.Name.Trim();
            c.AgeGroup = r.AgeGroup;
            c.Capacity = r.Capacity;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ---------- children + guardians ----------
        any.MapGet("/children", async (AppDb db, ClaimsPrincipal p, string? search, Guid? classroomId, Guid? staffId, bool? active, int? page, int? pageSize) =>
        {
            var q = Visible(db, p).OrderBy(c => c.FirstName);
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(c => (c.FirstName + " " + c.LastName).ToLower().Contains(search.ToLower())).OrderBy(c => c.FirstName);
            if (classroomId is not null)
                q = q.Where(c => c.ClassroomId == classroomId).OrderBy(c => c.FirstName);
            if (staffId is not null)
                q = q.Where(c => db.StaffClassrooms.Any(s => s.UserId == staffId && s.ClassroomId == c.ClassroomId))
                    .OrderBy(c => c.FirstName);
            if (active is not null)
                q = q.Where(c => c.IsActive == active).OrderBy(c => c.FirstName);
            var paged = page is not null || pageSize is not null;
            var pno = Math.Max(page ?? 1, 1); var size = Math.Clamp(pageSize ?? 25, 1, 100);
            var total = await q.CountAsync();
            if (paged) q = q.Skip((pno - 1) * size).Take(size).OrderBy(c => c.FirstName);
            if (p.RoleName() == Roles.Parent)
            {
                var items = await q.Select(c => new
                {
                    c.Id, c.FirstName, c.LastName, c.DateOfBirth, c.ClassroomId, Classroom = c.Classroom.Name,
                    c.Allergies, c.MedicalNotes, c.IsActive,
                }).ToListAsync();
                return paged ? Results.Ok(new { items, total, page = pno, pageSize = size }) : Results.Ok(items);
            }
            var adminItems = await q.Select(c => new
            {
                c.Id, c.FirstName, c.LastName, c.DateOfBirth, c.ClassroomId, Classroom = c.Classroom.Name,
                c.Allergies, c.MedicalNotes, c.IsActive,
                Staff = db.StaffClassrooms.Where(s => s.ClassroomId == c.ClassroomId)
                    .Select(s => new { s.UserId, Name = db.Users.Where(u => u.Id == s.UserId).Select(u => u.FullName).FirstOrDefault() })
                    .ToList(),
                Guardians = db.ChildGuardians.Where(g => g.ChildId == c.Id)
                .Select(g => new { g.GuardianId, g.Guardian.FullName, g.Guardian.Phone, g.Relationship, g.IsPrimary, g.CanPickup, g.CanViewReports }).ToList(),
            }).ToListAsync();
            return paged ? Results.Ok(new { items = adminItems, total, page = pno, pageSize = size }) : Results.Ok(adminItems);
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
            if (string.IsNullOrWhiteSpace(r.FirstName) || string.IsNullOrWhiteSpace(r.LastName)) return Bad("First and last name are required");
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
                    IsPrimary = r.IsPrimary, CanPickup = r.CanPickup, CanViewReports = r.CanViewReports,
                });
            await db.SaveChangesAsync();
            return Results.Ok(new { g.Id });
        });

        adm.MapPut("/guardians/{id:guid}", async (Guid id, GuardianReq r, AppDb db, ClaimsPrincipal p) =>
        {
            var g = await db.Guardians.FirstOrDefaultAsync(x => x.Id == id && x.CenterId == p.Cid());
            if (g is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(r.FullName)) return Bad("Guardian name is required");
            var email = r.Email?.Trim().ToLower();
            User? u = g.UserId is Guid uid ? await db.Users.FirstOrDefaultAsync(x => x.Id == uid) : null;
            if (email is not null && await db.Users.AnyAsync(x => x.Email == email && x.Id != (g.UserId ?? Guid.Empty)))
                return Conflict("Email is already used");
            g.FullName = r.FullName.Trim(); g.Phone = r.Phone; g.Email = email;
            if (u is not null)
            {
                u.FullName = g.FullName; u.Phone = g.Phone; u.Email = email ?? u.Email;
                if (!string.IsNullOrWhiteSpace(r.Password))
                {
                    if (r.Password.Length < 6) return Bad("Password must be at least 6 characters");
                    u.PasswordHash = Hash(r.Password);
                }
            }
            else if (!string.IsNullOrWhiteSpace(r.Password))
            {
                if (email is null) return Bad("Email is required to create a login");
                if (r.Password.Length < 6) return Bad("Password must be at least 6 characters");
                u = new User { CenterId = g.CenterId, Email = email, PasswordHash = Hash(r.Password), Role = Roles.Parent, FullName = g.FullName, Phone = g.Phone };
                db.Users.Add(u); g.UserId = u.Id;
            }
            await db.SaveChangesAsync();
            return Results.NoContent();
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
                IsPrimary = r.IsPrimary, CanPickup = r.CanPickup, CanViewReports = r.CanViewReports,
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
        staffOps.MapPost("/attendance/check-in", async (CheckInReq r, AppDb db, ClaimsPrincipal p) =>
        {
            if (!await Visible(db, p).AnyAsync(c => c.Id == r.ChildId && c.IsActive)) return Results.NotFound();
            if (await db.Attendance.AnyAsync(a => a.ChildId == r.ChildId && a.CheckOutAt == null))
                return Conflict("Child is already checked in");
            db.Attendance.Add(new Attendance { CenterId = p.Cid()!.Value, ChildId = r.ChildId, CheckedInBy = p.Uid() });
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        staffOps.MapPost("/attendance/{id:guid}/check-out", async (Guid id, CheckOutReq r, AppDb db, ClaimsPrincipal p) =>
        {
            var ids = Visible(db, p).Select(c => c.Id);
            var a = await db.Attendance.FirstOrDefaultAsync(x => x.Id == id && x.CheckOutAt == null && ids.Contains(x.ChildId));
            if (a is null) return Results.NotFound();
            var eligibleGuardians = await db.ChildGuardians.CountAsync(x => x.ChildId == a.ChildId && x.CanPickup);
            if (eligibleGuardians > 0 && r.PickedUpByGuardianId is null)
                return Bad("Select the parent or guardian who picked up the child");
            a.CheckOutAt = DateTime.UtcNow;
            a.CheckedOutBy = p.Uid();
            if (r.PickedUpByGuardianId is not Guid pickupGuardianId ||
                !await db.ChildGuardians.AnyAsync(x => x.ChildId == a.ChildId && x.GuardianId == pickupGuardianId && x.CanPickup))
                return Bad("The selected pickup person is not authorized for this child");
            a.PickedUpByGuardianId = pickupGuardianId;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ?date=YYYY-MM-DD -> that day (plus any visit still open); no date -> last 30 days
        any.MapGet("/attendance", async (AppDb db, ClaimsPrincipal p, DateOnly? date, Guid? childId, string? search, int? page, int? pageSize) =>
        {
            var ids = Visible(db, p).Select(c => c.Id);
            var q = db.Attendance.Where(a => ids.Contains(a.ChildId));
            if (childId != null) q = q.Where(a => a.ChildId == childId);
            if (!string.IsNullOrWhiteSpace(search))
                q = q.Where(a => (a.Child.FirstName + " " + a.Child.LastName).ToLower().Contains(search.ToLower()));
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
            var paged = page is not null || pageSize is not null;
            var pno = Math.Max(page ?? 1, 1); var size = Math.Clamp(pageSize ?? 25, 1, 100);
            var total = await q.CountAsync();
            IQueryable<Attendance> rows = q.OrderByDescending(a => a.CheckInAt);
            rows = paged ? rows.Skip((pno - 1) * size).Take(size) : rows.Take(300);
            var items = await rows.Select(a => new
            {
                a.Id, a.ChildId, Child = a.Child.FirstName + " " + a.Child.LastName, a.CheckInAt, a.CheckOutAt,
                PickedUpBy = a.PickedUpByGuardian != null ? a.PickedUpByGuardian.FullName : null,
                PickedUpByPhone = a.PickedUpByGuardian != null ? a.PickedUpByGuardian.Phone : null,
                CheckedInBy = db.Users.Where(u => u.Id == a.CheckedInBy).Select(u => u.FullName).FirstOrDefault(),
                CheckedOutBy = a.CheckedOutBy == null ? null : db.Users.Where(u => u.Id == a.CheckedOutBy).Select(u => u.FullName).FirstOrDefault(),
            }).ToListAsync();
            return paged ? Results.Ok(new { items, total, page = pno, pageSize = size }) : Results.Ok(items);
        });

        // ---------- daily reports ----------
        staffOps.MapPost("/reports/entries", async (NewEntry r, AppDb db, ClaimsPrincipal p) =>
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

        staffOps.MapDelete("/reports/entries/{id:guid}", async (Guid id, AppDb db, ClaimsPrincipal p) =>
        {
            var ids = Visible(db, p).Select(c => c.Id);
            var e = await db.Entries.FirstOrDefaultAsync(x => x.Id == id && ids.Contains(x.Report.ChildId));
            if (e is null) return Results.NotFound();
            db.Entries.Remove(e);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // save the summary note; publish = true makes the report visible to parents
        staffOps.MapPut("/reports/{id:guid}", async (Guid id, ReportReq r, AppDb db, ClaimsPrincipal p) =>
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
            if (p.RoleName() == Roles.Parent)
                q = q.Where(r => r.PublishedAt != null &&
                    db.ChildGuardians.Any(g => g.ChildId == r.ChildId && g.Guardian.UserId == p.Uid() && g.CanViewReports));
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

        ops.MapPut("/announcements/{id:guid}", async (Guid id, AnnReq r, AppDb db, ClaimsPrincipal p) =>
        {
            var a = await db.Announcements.FirstOrDefaultAsync(x => x.Id == id);
            if (a is null) return Results.NotFound();
            if (p.RoleName() == Roles.Staff && a.AuthorId != p.Uid()) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(r.Title) || string.IsNullOrWhiteSpace(r.Body)) return Bad("Title and message are required");
            if (r.ClassroomId is Guid room)
            {
                if (!await db.Classrooms.AnyAsync(c => c.Id == room)) return Results.NotFound();
                if (p.RoleName() == Roles.Staff && !await db.StaffClassrooms.AnyAsync(s => s.UserId == p.Uid() && s.ClassroomId == room))
                    return Results.Forbid();
            }
            else if (p.RoleName() == Roles.Staff) return Results.Forbid();
            a.Title = r.Title.Trim(); a.Body = r.Body.Trim(); a.ClassroomId = r.ClassroomId;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        ops.MapDelete("/announcements/{id:guid}", async (Guid id, AppDb db, ClaimsPrincipal p) =>
        {
            var a = await db.Announcements.FirstOrDefaultAsync(x => x.Id == id);
            if (a is null) return Results.NotFound();
            if (p.RoleName() == Roles.Staff &&
                (a.AuthorId != p.Uid() ||
                 a.ClassroomId is not Guid classroomId ||
                 !await db.StaffClassrooms.AnyAsync(s => s.UserId == p.Uid() && s.ClassroomId == classroomId)))
                return Results.Forbid();
            db.Announcements.Remove(a);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ---------- private messages ----------
        any.MapGet("/messaging/recipients", async (AppDb db, ClaimsPrincipal p) =>
        {
            var uid = p.Uid();
            var centerId = p.Cid();
            var users = db.Users.Where(u => u.CenterId == centerId && u.IsActive && u.Id != uid);
            if (p.RoleName() == Roles.Parent)
            {
                var classrooms = db.ChildGuardians
                    .Where(g => g.Guardian.UserId == uid)
                    .Select(g => g.Child.ClassroomId);
                users = users.Where(u => u.Role == Roles.Admin ||
                    (u.Role == Roles.Staff && db.StaffClassrooms.Any(s =>
                        s.UserId == u.Id && classrooms.Contains(s.ClassroomId))));
            }
            else if (p.RoleName() == Roles.Staff)
            {
                var classrooms = db.StaffClassrooms.Where(s => s.UserId == uid).Select(s => s.ClassroomId);
                var parentIds = db.ChildGuardians
                    .Where(g => classrooms.Contains(g.Child.ClassroomId) && g.Guardian.UserId != null)
                    .Select(g => g.Guardian.UserId!.Value);
                users = users.Where(u => u.Role == Roles.Admin || parentIds.Contains(u.Id));
            }
            else if (p.RoleName() == Roles.Admin)
            {
                users = users.Where(u => u.Role == Roles.Staff ||
                    (u.Role == Roles.Parent && db.Guardians.Any(g => g.UserId == u.Id)));
            }
            else
            {
                users = users.Where(u => false);
            }

            return await users.OrderBy(u => u.FullName)
                .Select(u => new { u.Id, u.FullName, u.Role })
                .ToListAsync();
        });

        any.MapGet("/messages", async (AppDb db, ClaimsPrincipal p) =>
        {
            var uid = p.Uid();
            return await db.Conversations
                .Where(c => c.ParentUserId == uid || c.StaffUserId == uid)
                .OrderByDescending(c => c.UpdatedAt)
                .Select(c => new
                {
                    c.Id, c.Subject, c.UpdatedAt, c.ChildId,
                    OtherUser = c.ParentUserId == uid ? c.StaffUser.FullName : c.ParentUser.FullName,
                    OtherRole = c.ParentUserId == uid ? c.StaffUser.Role : c.ParentUser.Role,
                    Child = c.Child == null ? null : c.Child.FirstName + " " + c.Child.LastName,
                    Unread = c.Messages.Count(m => m.SenderId != uid && m.ReadAt == null),
                    LastMessage = c.Messages.OrderByDescending(m => m.SentAt)
                        .Select(m => m.Body).FirstOrDefault(),
                }).ToListAsync();
        });

        any.MapGet("/messages/{id:guid}", async (Guid id, AppDb db, ClaimsPrincipal p) =>
        {
            var uid = p.Uid();
            var conversation = await db.Conversations
                .Include(c => c.Messages).ThenInclude(m => m.Sender)
                .Include(c => c.Child)
                .Include(c => c.ParentUser)
                .Include(c => c.StaffUser)
                .FirstOrDefaultAsync(c => c.Id == id && (c.ParentUserId == uid || c.StaffUserId == uid));
            if (conversation is null) return Results.NotFound();

            var unread = conversation.Messages.Where(m => m.SenderId != uid && m.ReadAt == null).ToList();
            foreach (var message in unread) message.ReadAt = DateTime.UtcNow;
            if (unread.Count > 0) await db.SaveChangesAsync();

            return Results.Ok(new
            {
                conversation.Id, conversation.Subject, conversation.ChildId,
                Child = conversation.Child == null ? null : conversation.Child.FirstName + " " + conversation.Child.LastName,
                OtherUser = conversation.ParentUserId == uid ? conversation.StaffUser.FullName : conversation.ParentUser.FullName,
                Messages = conversation.Messages.OrderBy(m => m.SentAt)
                    .Select(m => new { m.Id, m.Body, m.SentAt, m.SenderId, Sender = m.Sender.FullName, SenderRole = m.Sender.Role }),
            });
        });

        any.MapPost("/messages", async (MessageReq r, AppDb db, ClaimsPrincipal p) =>
        {
            if (string.IsNullOrWhiteSpace(r.Body) || string.IsNullOrWhiteSpace(r.Subject))
                return Bad("Subject and message are required");
            var senderId = p.Uid();
            var senderRole = p.RoleName();
            var recipient = await db.Users.FirstOrDefaultAsync(u =>
                u.Id == r.RecipientId && u.CenterId == p.Cid() && u.IsActive);
            if (recipient is null || recipient.Id == senderId) return Bad("That recipient is not available");

            Guid parentId;
            Guid staffId;
            if (senderRole == Roles.Parent && recipient.Role is Roles.Admin or Roles.Staff)
            {
                parentId = senderId; staffId = recipient.Id;
                if (recipient.Role == Roles.Staff && r.ChildId is not Guid childId)
                    return Bad("Choose a child when messaging classroom staff");
                if (recipient.Role == Roles.Staff && !await db.StaffClassrooms.AnyAsync(s =>
                        s.UserId == recipient.Id && db.Children.Any(c => c.Id == r.ChildId && c.ClassroomId == s.ClassroomId)))
                    return Results.Forbid();
            }
            else if (senderRole is Roles.Admin or Roles.Staff && recipient.Role == Roles.Parent)
            {
                parentId = recipient.Id; staffId = senderId;
                if (senderRole == Roles.Staff)
                {
                    if (r.ChildId is not Guid childId ||
                        !await db.StaffClassrooms.AnyAsync(s =>
                            s.UserId == senderId && db.Children.Any(c => c.Id == childId && c.ClassroomId == s.ClassroomId)))
                        return Results.Forbid();
                }
            }
            else return Results.Forbid();

            if (r.ChildId is Guid selectedChild &&
                !await db.ChildGuardians.AnyAsync(g => g.ChildId == selectedChild && g.Guardian.UserId == parentId))
                return Bad("That child is not linked to the parent");

            var conversation = new Conversation
            {
                CenterId = p.Cid()!.Value, ParentUserId = parentId, StaffUserId = staffId,
                ChildId = r.ChildId, Subject = r.Subject.Trim(),
            };
            conversation.Messages.Add(new Message { SenderId = senderId, Body = r.Body.Trim() });
            db.Conversations.Add(conversation);
            await db.SaveChangesAsync();
            return Results.Ok(new { conversation.Id });
        });

        any.MapPost("/messages/{id:guid}/reply", async (Guid id, ReplyReq r, AppDb db, ClaimsPrincipal p) =>
        {
            if (string.IsNullOrWhiteSpace(r.Body)) return Bad("Message is required");
            var uid = p.Uid();
            var conversation = await db.Conversations.FirstOrDefaultAsync(c =>
                c.Id == id && (c.ParentUserId == uid || c.StaffUserId == uid));
            if (conversation is null) return Results.NotFound();
            db.Messages.Add(new Message
            {
                ConversationId = conversation.Id,
                SenderId = uid,
                Body = r.Body.Trim(),
            });
            conversation.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        any.MapGet("/dashboard", async Task<IResult> (AppDb db, ClaimsPrincipal p) =>
        {
            if (p.RoleName() == Roles.SuperAdmin)
            {
                var centers = db.Centers.AsNoTracking();
                return Results.Ok(new
                {
                    Children = await db.Children.IgnoreQueryFilters().CountAsync(c => c.IsActive),
                    Staff = await db.Users.IgnoreQueryFilters().CountAsync(u => u.Role == Roles.Staff && u.IsActive),
                    Centers = await centers.CountAsync(),
                    ActiveCenters = await centers.CountAsync(c => c.IsActive),
                });
            }
            var children = Visible(db, p);
            var childIds = children.Select(c => c.Id);
            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(EatHours));
            var (dayStart, dayEnd) = Day(today);
            var now = DateTime.UtcNow;
            var enrolled = await children.CountAsync(c => c.IsActive);
            var checkedIn = await db.Attendance.CountAsync(a =>
                childIds.Contains(a.ChildId) && a.CheckInAt <= now && a.CheckOutAt == null);
            var checkedOutToday = await db.Attendance.CountAsync(a =>
                childIds.Contains(a.ChildId) && a.CheckOutAt >= dayStart && a.CheckOutAt < dayEnd);
            var reportsNotSharedToday = await children
                .Where(c => c.IsActive && !db.DailyReports.Any(r =>
                    r.ChildId == c.Id && r.ReportDate == today && r.PublishedAt != null))
                .CountAsync();
            return Results.Ok(new
            {
                Children = enrolled,
                ActiveChildren = enrolled,
                Enrolled = enrolled,
                Classrooms = p.RoleName() == Roles.Parent ? 0 : await db.Classrooms.CountAsync(),
                Staff = p.RoleName() == Roles.Admin ? await db.Users.CountAsync(u => u.CenterId == p.Cid() && u.Role == Roles.Staff && u.IsActive) : 0,
                CheckedIn = checkedIn,
                PresentNow = checkedIn,
                CheckedOutToday = checkedOutToday,
                ReportsNotSharedToday = reportsNotSharedToday,
                Announcements = await db.Announcements.CountAsync(),
            });
        });
    }
}

// ---------- request bodies ----------
public record LoginReq(string? Email, string? Password);
public record ForgotPasswordReq(string? Email);
public record ResetPasswordReq(string? Token, string? NewPassword);
public record NewCenter(string Name, string? Address, string? Phone, string AdminName, string AdminEmail, string? AdminPassword);
public record CenterReq(string Name, string? Address, string? Phone, Guid? AdminId);
public record AdminUpdate(string FullName, string Email, string? Phone, bool IsActive);
public record AdminUserUpdate(string? FullName, string? Email, string? Phone, Guid? CenterId, bool IsActive);
public record ActiveReq(bool IsActive);

// Password is the original API property. NewPassword is accepted as an alias so
// clients can use the same payload shape as the current password-change endpoint.
public record ChangePasswordReq(string? CurrentPassword, string? NewPassword);
public record NewStaff(string FullName, string Email, string? Password, string? Phone, string? JobTitle, List<Guid>? ClassroomIds);
public record StaffUpdate(string? FullName, string? Email, string? Phone, string? JobTitle, string? Password, bool IsActive, List<Guid>? ClassroomIds);
public record ClassroomReq(string Name, string? AgeGroup, int Capacity);
public record ChildReq(string FirstName, string LastName, DateOnly DateOfBirth, Guid ClassroomId, string? Allergies, string? MedicalNotes, bool? IsActive);
public record GuardianReq(string FullName, string? Phone, string? Email, string? Password, Guid? ChildId, string? Relationship, bool IsPrimary, bool CanPickup = true, bool CanViewReports = true);
public record LinkReq(Guid GuardianId, string? Relationship, bool IsPrimary, bool CanPickup = true, bool CanViewReports = true);
public record CheckInReq(Guid ChildId);
public record CheckOutReq(Guid? PickedUpByGuardianId);
public record NewEntry(Guid ChildId, DateOnly Date, string EntryType, string? Details, DateTime? OccurredAt);
public record ReportReq(string? GeneralNote, bool Publish);
public record AnnReq(string Title, string Body, Guid? ClassroomId);
public record MessageReq(Guid RecipientId, Guid? ChildId, string? Subject, string? Body);
public record ReplyReq(string? Body);
