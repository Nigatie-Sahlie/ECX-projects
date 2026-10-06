namespace DaycareApp.Api.Domain;

public enum Role { Admin, Staff, Parent }
public enum LogType { Meal, Nap, Activity, Incident }

public class Daycare
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DaycareId { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string PasswordHash { get; set; } = "";
    public Role Role { get; set; }
}

public class Child
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DaycareId { get; set; }
    public Guid ParentId { get; set; }
    public string Name { get; set; } = "";
    public DateOnly DateOfBirth { get; set; }
    public string? Allergies { get; set; }
}

public class Attendance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChildId { get; set; }
    public DateOnly Date { get; set; }
    public DateTime CheckIn { get; set; }
    public DateTime? CheckOut { get; set; }
}

public class DailyLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChildId { get; set; }
    public DateOnly Date { get; set; }
    public DateTime At { get; set; }
    public LogType Type { get; set; }
    public string Notes { get; set; } = "";
    public Guid CreatedById { get; set; }
}

public class Announcement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DaycareId { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
