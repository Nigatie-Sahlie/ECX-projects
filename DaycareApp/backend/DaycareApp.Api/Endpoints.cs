using System.Security.Claims;
using DaycareApp.Api.Data;
using DaycareApp.Api.Domain;
using DaycareApp.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DaycareApp.Api;

public record LoginReq(string Email, string Password);
public record CreateParentReq(string Name, string Email, string? Phone, string Password);
public record CreateChildReq(string Name, DateOnly DateOfBirth, string? Allergies, Guid ParentId);
public record ChildIdReq(Guid ChildId);
public record LogReq(Guid ChildId, LogType Type, string Notes);
public record AnnouncementReq(string Title, string Body);

public static class Endpoints
{
    static Guid Uid(this ClaimsPrincipal u) => Guid.Parse(u.FindFirstValue("sub")!);
    static Guid Dc(this ClaimsPrincipal u) => Guid.Parse(u.FindFirstValue("dc")!);
    static bool IsParent(this ClaimsPrincipal u) => u.IsInRole("Parent");

    public static void MapApi(this WebApplication app)
    {
        var api = app.MapGroup("/api");
        var any = api.MapGroup("").RequireAuthorization();
        var staff = api.MapGroup("").RequireAuthorization("Staff");

        api.MapPost("/auth/login", async (LoginReq r, AppDbContext db, IPasswordHasher<User> h, TokenService t) =>
        {
            var email = r.Email.Trim().ToLowerInvariant();
            var u = await db.Users.FirstOrDefaultAsync(x => x.Email == email);
            if (u is null || h.VerifyHashedPassword(u, u.PasswordHash, r.Password) == PasswordVerificationResult.Failed)
                return Results.Unauthorized();
            return Results.Ok(new { token = t.Create(u), user = new { u.Id, u.Name, role = u.Role.ToString() } });
        });

        // ---- Parents
        staff.MapGet("/parents", async (ClaimsPrincipal u, AppDbContext db) =>
        {
            var dc = u.Dc();
            return await db.Users.AsNoTracking()
                .Where(x => x.DaycareId == dc && x.Role == Role.Parent)
                .OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.Email, x.Phone }).ToListAsync();
        });

        staff.MapPost("/parents", async (CreateParentReq r, ClaimsPrincipal u, AppDbContext db, IPasswordHasher<User> h) =>
        {
            var email = r.Email.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(r.Name) || r.Password.Length < 8)
                return Results.BadRequest(new { error = "Name is required and the password needs at least 8 characters." });
            if (await db.Users.AnyAsync(x => x.Email == email))
                return Results.Conflict(new { error = "A user with this email already exists." });
            var p = new User { DaycareId = u.Dc(), Name = r.Name.Trim(), Email = email, Phone = r.Phone, Role = Role.Parent };
            p.PasswordHash = h.HashPassword(p, r.Password);
            db.Users.Add(p);
            await db.SaveChangesAsync();
            return Results.Ok(new { p.Id, p.Name, p.Email, p.Phone });
        });

        // ---- Children
        any.MapGet("/children", async (ClaimsPrincipal u, AppDbContext db, int page = 1, int pageSize = 50) =>
        {
            var dc = u.Dc(); var uid = u.Uid(); var parent = u.IsParent();
            var size = Math.Clamp(pageSize, 1, 100); var skip = (Math.Max(page, 1) - 1) * size;
            var q = db.Children.AsNoTracking().Where(c => c.DaycareId == dc);
            if (parent) q = q.Where(c => c.ParentId == uid);
            return await q.OrderBy(c => c.Name).Skip(skip).Take(size)
                .Select(c => new { c.Id, c.Name, c.DateOfBirth, c.Allergies, c.ParentId }).ToListAsync();
        });

        staff.MapPost("/children", async (CreateChildReq r, ClaimsPrincipal u, AppDbContext db) =>
        {
            var dc = u.Dc();
            if (string.IsNullOrWhiteSpace(r.Name)) return Results.BadRequest(new { error = "Name is required." });
            if (!await db.Users.AnyAsync(x => x.Id == r.ParentId && x.DaycareId == dc && x.Role == Role.Parent))
                return Results.BadRequest(new { error = "Choose a parent from this daycare." });
            var c = new Child { DaycareId = dc, ParentId = r.ParentId, Name = r.Name.Trim(), DateOfBirth = r.DateOfBirth, Allergies = r.Allergies };
            db.Children.Add(c);
            await db.SaveChangesAsync();
            return Results.Ok(new { c.Id, c.Name, c.DateOfBirth, c.Allergies, c.ParentId });
        });

        any.MapGet("/children/{id:guid}/report", async (Guid id, DateOnly? date, ClaimsPrincipal u, AppDbContext db, Clock clock) =>
        {
            var dc = u.Dc();
            var child = await db.Children.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && c.DaycareId == dc);
            if (child is null || (u.IsParent() && child.ParentId != u.Uid())) return Results.NotFound();
            var d = date ?? clock.Today();
            var att = await db.Attendance.AsNoTracking().FirstOrDefaultAsync(a => a.ChildId == id && a.Date == d);
            var logs = await db.DailyLogs.AsNoTracking().Where(l => l.ChildId == id && l.Date == d)
                .OrderBy(l => l.At).Select(l => new { l.Id, l.Type, l.Notes, l.At }).ToListAsync();
            return Results.Ok(new { child = new { child.Id, child.Name }, date = d, checkIn = att?.CheckIn, checkOut = att?.CheckOut, logs });
        });

        // ---- Attendance
        staff.MapGet("/attendance/today", async (ClaimsPrincipal u, AppDbContext db, Clock clock) =>
        {
            var dc = u.Dc(); var today = clock.Today();
            var kids = await db.Children.AsNoTracking().Where(c => c.DaycareId == dc).OrderBy(c => c.Name)
                .Select(c => new { c.Id, c.Name, c.Allergies }).ToListAsync();
            var att = await db.Attendance.AsNoTracking()
                .Where(a => a.Date == today && db.Children.Any(c => c.Id == a.ChildId && c.DaycareId == dc))
                .ToDictionaryAsync(a => a.ChildId);
            return kids.Select(k => new
            {
                k.Id, k.Name, k.Allergies,
                checkIn = att.TryGetValue(k.Id, out var a) ? a.CheckIn : (DateTime?)null,
                checkOut = att.TryGetValue(k.Id, out var b) ? b.CheckOut : null
            });
        });

        staff.MapPost("/attendance/check-in", async (ChildIdReq r, ClaimsPrincipal u, AppDbContext db, Clock clock) =>
        {
            var dc = u.Dc(); var today = clock.Today();
            if (!await db.Children.AnyAsync(c => c.Id == r.ChildId && c.DaycareId == dc)) return Results.NotFound();
            if (await db.Attendance.AnyAsync(a => a.ChildId == r.ChildId && a.Date == today))
                return Results.Conflict(new { error = "Already checked in today." });
            db.Attendance.Add(new Attendance { ChildId = r.ChildId, Date = today, CheckIn = DateTime.UtcNow });
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        staff.MapPost("/attendance/check-out", async (ChildIdReq r, ClaimsPrincipal u, AppDbContext db, Clock clock) =>
        {
            var dc = u.Dc(); var today = clock.Today();
            var a = await db.Attendance.FirstOrDefaultAsync(x => x.ChildId == r.ChildId && x.Date == today
                && db.Children.Any(c => c.Id == x.ChildId && c.DaycareId == dc));
            if (a is null) return Results.NotFound(new { error = "Not checked in today." });
            if (a.CheckOut is not null) return Results.Conflict(new { error = "Already checked out." });
            a.CheckOut = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        // ---- Daily logs
        staff.MapPost("/logs", async (LogReq r, ClaimsPrincipal u, AppDbContext db, Clock clock) =>
        {
            var dc = u.Dc();
            if (!await db.Children.AnyAsync(c => c.Id == r.ChildId && c.DaycareId == dc)) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(r.Notes)) return Results.BadRequest(new { error = "Add a short note." });
            db.DailyLogs.Add(new DailyLog { ChildId = r.ChildId, Date = clock.Today(), At = DateTime.UtcNow, Type = r.Type, Notes = r.Notes.Trim(), CreatedById = u.Uid() });
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        // ---- Announcements
        any.MapGet("/announcements", async (ClaimsPrincipal u, AppDbContext db) =>
        {
            var dc = u.Dc();
            return await db.Announcements.AsNoTracking().Where(a => a.DaycareId == dc)
                .OrderByDescending(a => a.CreatedAt).Take(20)
                .Select(a => new { a.Id, a.Title, a.Body, a.CreatedAt }).ToListAsync();
        });

        staff.MapPost("/announcements", async (AnnouncementReq r, ClaimsPrincipal u, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(r.Title) || string.IsNullOrWhiteSpace(r.Body))
                return Results.BadRequest(new { error = "Title and message are required." });
            var a = new Announcement { DaycareId = u.Dc(), Title = r.Title.Trim(), Body = r.Body.Trim(), CreatedAt = DateTime.UtcNow };
            db.Announcements.Add(a);
            await db.SaveChangesAsync();
            return Results.Ok(new { a.Id, a.Title, a.Body, a.CreatedAt });
        });
    }
}
