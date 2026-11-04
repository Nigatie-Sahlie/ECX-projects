namespace DaycareApi;

public static class Roles
{
    public const string SuperAdmin = "super_admin", Admin = "admin", Staff = "staff", Parent = "parent";
}

public class Center
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? CenterId { get; set; } // null only for super_admin
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PasswordResetToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class StaffProfile
{
    public Guid UserId { get; set; }
    public string? JobTitle { get; set; }
    public DateOnly? HireDate { get; set; }
}

public class Classroom
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CenterId { get; set; }
    public string Name { get; set; } = "";
    public string? AgeGroup { get; set; }
    public int Capacity { get; set; }
}

public class StaffClassroom
{
    public Guid UserId { get; set; }
    public Guid ClassroomId { get; set; }
    public Classroom Classroom { get; set; } = null!;
}

public class Guardian
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CenterId { get; set; }
    public Guid? UserId { get; set; } // set when the guardian has a login
    public string FullName { get; set; } = "";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
}

public class Child
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CenterId { get; set; }
    public Guid ClassroomId { get; set; }
    public Classroom Classroom { get; set; } = null!;
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateOnly DateOfBirth { get; set; }
    public string? Allergies { get; set; }
    public string? MedicalNotes { get; set; }
    public string? PhotoUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
}

public class ChildGuardian
{
    public Guid ChildId { get; set; }
    public Child Child { get; set; } = null!;
    public Guid GuardianId { get; set; }
    public Guardian Guardian { get; set; } = null!;
    public string Relationship { get; set; } = "Parent";
    public bool IsPrimary { get; set; }
    public bool CanPickup { get; set; } = true;
    public bool CanViewReports { get; set; } = true;
}

public class Attendance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CenterId { get; set; }
    public Guid ChildId { get; set; }
    public Child Child { get; set; } = null!;
    public DateTime CheckInAt { get; set; } = DateTime.UtcNow;
    public Guid CheckedInBy { get; set; }
    public DateTime? CheckOutAt { get; set; }
    public Guid? CheckedOutBy { get; set; }
    public Guid? PickedUpByGuardianId { get; set; }
    public Guardian? PickedUpByGuardian { get; set; }
    public string? Notes { get; set; }
}

public class DailyReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CenterId { get; set; }
    public Guid ChildId { get; set; }
    public Child Child { get; set; } = null!;
    public DateOnly ReportDate { get; set; }
    public Guid CreatedBy { get; set; }
    public string? GeneralNote { get; set; }
    public DateTime? PublishedAt { get; set; }
    public List<DailyReportEntry> Entries { get; set; } = new();
}

public class DailyReportEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DailyReportId { get; set; }
    public DailyReport Report { get; set; } = null!;
    public string EntryType { get; set; } = "note"; // meal | nap | diaper | activity | mood | note
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string? Details { get; set; }
    public Guid CreatedBy { get; set; }
}

public class Announcement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CenterId { get; set; }
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;
    public Guid? ClassroomId { get; set; } // null = whole center
    public Classroom? Classroom { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
}

public class ScheduleItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CenterId { get; set; }
    public Guid? ClassroomId { get; set; }
    public Classroom? Classroom { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Recurrence { get; set; } = "daily"; // daily | weekly | monthly
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public int? DayOfWeek { get; set; } // Sunday = 0 through Saturday = 6
    public int? DayOfMonth { get; set; } // 1 through 31
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class EventItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CenterId { get; set; }
    public Guid? ClassroomId { get; set; }
    public Classroom? Classroom { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Recurrence { get; set; } = "one_time"; // one_time | daily | weekly | monthly
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public int? DayOfWeek { get; set; }
    public int? DayOfMonth { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CenterId { get; set; }
    public Guid ParentUserId { get; set; }
    public Guid StaffUserId { get; set; }
    public Guid? ChildId { get; set; }
    public string Subject { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public User ParentUser { get; set; } = null!;
    public User StaffUser { get; set; } = null!;
    public Child? Child { get; set; }
    public List<Message> Messages { get; set; } = new();
}

public class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public string Body { get; set; } = "";
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public User Sender { get; set; } = null!;
}

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid CenterId { get; set; }
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string? Link { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
    public User User { get; set; } = null!;
}
