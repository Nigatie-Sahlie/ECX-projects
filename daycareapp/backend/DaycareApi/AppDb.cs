using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace DaycareApi;

public class AppDb(DbContextOptions<AppDb> options, IHttpContextAccessor http) : DbContext(options)
{
    // Center of the signed-in user. Null for super_admin / background work (seeding).
    // Every tenant table is filtered by this, so one daycare can never read another's data.
    Guid? CurrentCenterId => Guid.TryParse(http.HttpContext?.User.FindFirstValue("cid"), out var g) ? g : null;

    public DbSet<Center> Centers => Set<Center>();
    public DbSet<User> Users => Set<User>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<StaffProfile> StaffProfiles => Set<StaffProfile>();
    public DbSet<Classroom> Classrooms => Set<Classroom>();
    public DbSet<StaffClassroom> StaffClassrooms => Set<StaffClassroom>();
    public DbSet<Guardian> Guardians => Set<Guardian>();
    public DbSet<Child> Children => Set<Child>();
    public DbSet<ChildGuardian> ChildGuardians => Set<ChildGuardian>();
    public DbSet<Attendance> Attendance => Set<Attendance>();
    public DbSet<DailyReport> DailyReports => Set<DailyReport>();
    public DbSet<DailyReportEntry> Entries => Set<DailyReportEntry>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<ScheduleItem> Schedules => Set<ScheduleItem>();
    public DbSet<EventItem> Events => Set<EventItem>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // keys
        b.Entity<StaffProfile>().HasKey(x => x.UserId);
        b.Entity<StaffClassroom>().HasKey(x => new { x.UserId, x.ClassroomId });
        b.Entity<ChildGuardian>().HasKey(x => new { x.ChildId, x.GuardianId });

        // constraints + indexes
        b.Entity<User>().HasIndex(u => u.Email).IsUnique();
        b.Entity<PasswordResetToken>().HasIndex(x => x.TokenHash).IsUnique();
        b.Entity<PasswordResetToken>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        b.Entity<User>().ToTable(t => t.HasCheckConstraint("ck_users_center",
            "\"Role\" = 'super_admin' OR \"CenterId\" IS NOT NULL"));
        b.Entity<DailyReport>().HasIndex(x => new { x.ChildId, x.ReportDate }).IsUnique();
        b.Entity<DailyReportEntry>().HasIndex(x => new { x.DailyReportId, x.OccurredAt });
        b.Entity<Attendance>().HasIndex(x => new { x.CenterId, x.CheckInAt });
        b.Entity<Attendance>().HasIndex(x => new { x.ChildId, x.CheckInAt });
        b.Entity<Announcement>().HasIndex(x => new { x.CenterId, x.PublishedAt });
        b.Entity<ScheduleItem>().HasIndex(x => new { x.CenterId, x.StartDate, x.Recurrence });
        b.Entity<EventItem>().HasIndex(x => new { x.CenterId, x.StartDate, x.Recurrence });
        b.Entity<ChildGuardian>().HasIndex(x => x.GuardianId);
        b.Entity<Conversation>().HasIndex(x => new { x.CenterId, x.UpdatedAt });
        b.Entity<Message>().HasIndex(x => new { x.ConversationId, x.SentAt });
        b.Entity<Notification>().HasIndex(x => new { x.UserId, x.CreatedAt });
        b.Entity<Notification>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);

        // foreign keys to users / centers that have no navigation property
        b.Entity<User>().HasOne<Center>().WithMany().HasForeignKey(x => x.CenterId);
        b.Entity<StaffProfile>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        b.Entity<StaffClassroom>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        b.Entity<Guardian>().HasOne<User>().WithMany().HasForeignKey(x => x.UserId);
        b.Entity<Attendance>().HasOne<User>().WithMany().HasForeignKey(x => x.CheckedInBy);
        b.Entity<Attendance>().HasOne<User>().WithMany().HasForeignKey(x => x.CheckedOutBy);
        b.Entity<DailyReport>().HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy);
        b.Entity<DailyReportEntry>().HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy);
        b.Entity<Conversation>().HasOne(x => x.ParentUser).WithMany().HasForeignKey(x => x.ParentUserId);
        b.Entity<Conversation>().HasOne(x => x.StaffUser).WithMany().HasForeignKey(x => x.StaffUserId);
        b.Entity<Conversation>().HasOne(x => x.Child).WithMany().HasForeignKey(x => x.ChildId);
        b.Entity<Message>().HasOne(x => x.Sender).WithMany().HasForeignKey(x => x.SenderId);

        // tenant isolation
        b.Entity<Classroom>().HasQueryFilter(x => x.CenterId == CurrentCenterId);
        b.Entity<Guardian>().HasQueryFilter(x => x.CenterId == CurrentCenterId);
        b.Entity<Child>().HasQueryFilter(x => x.CenterId == CurrentCenterId);
        b.Entity<Attendance>().HasQueryFilter(x => x.CenterId == CurrentCenterId);
        b.Entity<DailyReport>().HasQueryFilter(x => x.CenterId == CurrentCenterId);
        b.Entity<Announcement>().HasQueryFilter(x => x.CenterId == CurrentCenterId);
        b.Entity<ScheduleItem>().HasQueryFilter(x => x.CenterId == CurrentCenterId);
        b.Entity<EventItem>().HasQueryFilter(x => x.CenterId == CurrentCenterId);
        b.Entity<StaffClassroom>().HasQueryFilter(x => x.Classroom.CenterId == CurrentCenterId);
        b.Entity<ChildGuardian>().HasQueryFilter(x =>
            x.Child.CenterId == CurrentCenterId && x.Guardian.CenterId == CurrentCenterId);
        b.Entity<DailyReportEntry>().HasQueryFilter(x => x.Report.CenterId == CurrentCenterId);
        b.Entity<Conversation>().HasQueryFilter(x => x.CenterId == CurrentCenterId);
        b.Entity<Message>().HasQueryFilter(x => x.Conversation.CenterId == CurrentCenterId);
        b.Entity<Notification>().HasQueryFilter(x => x.CenterId == CurrentCenterId);

        // never cascade-delete: we deactivate instead (soft delete)
        foreach (var fk in b.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;
    }
}
