using DaycareApp.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace DaycareApp.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Daycare> Daycares => Set<Daycare>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Child> Children => Set<Child>();
    public DbSet<Attendance> Attendance => Set<Attendance>();
    public DbSet<DailyLog> DailyLogs => Set<DailyLog>();
    public DbSet<Announcement> Announcements => Set<Announcement>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Tenant-owned tables carry DaycareId (directly or via Child), so the schema can
        // host many daycares and be partitioned by it later. GUID keys avoid hot sequences.
        b.Entity<User>(e =>
        {
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Email).HasMaxLength(200);
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => new { x.DaycareId, x.Role });
        });
        b.Entity<Child>(e =>
        {
            e.HasIndex(x => new { x.DaycareId, x.Name });
            e.HasIndex(x => x.ParentId);
        });
        b.Entity<Attendance>(e =>
        {
            e.HasIndex(x => new { x.ChildId, x.Date }).IsUnique();
            e.HasIndex(x => x.Date);
        });
        b.Entity<DailyLog>(e =>
        {
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.ChildId, x.Date });
        });
        b.Entity<Announcement>(e =>
            e.HasIndex(x => new { x.DaycareId, x.CreatedAt }));
    }
}
