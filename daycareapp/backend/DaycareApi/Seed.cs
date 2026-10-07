namespace DaycareApi;

public static class Seed
{
    static string H(string p) => BCrypt.Net.BCrypt.HashPassword(p);

    public static void Run(AppDb db)
    {
        if (db.Users.Any(u => u.Role == Roles.SuperAdmin)) return;

        db.Users.Add(new User
        {
            Email = "super@daycare.local", PasswordHash = H("Super123!"),
            Role = Roles.SuperAdmin, FullName = "Platform Owner"
        });

        // demo daycare so you can click around immediately
        var center = new Center { Name = "Sunshine Daycare (demo)", Address = "Bole, Addis Ababa", Phone = "+251 11 000 0000" };
        var admin = new User { CenterId = center.Id, Email = "admin@demo.local", PasswordHash = H("Admin123!"), Role = Roles.Admin, FullName = "Demo Admin" };
        var staff = new User { CenterId = center.Id, Email = "staff@demo.local", PasswordHash = H("Staff123!"), Role = Roles.Staff, FullName = "Demo Teacher" };
        var parent = new User { CenterId = center.Id, Email = "parent@demo.local", PasswordHash = H("Parent123!"), Role = Roles.Parent, FullName = "Demo Parent" };
        var room = new Classroom { CenterId = center.Id, Name = "Sunflowers", AgeGroup = "2-3 years", Capacity = 12 };
        var child = new Child { CenterId = center.Id, ClassroomId = room.Id, FirstName = "Abel", LastName = "Tadesse", DateOfBirth = new DateOnly(2023, 5, 14), Allergies = "Peanuts" };
        var guardian = new Guardian { CenterId = center.Id, UserId = parent.Id, FullName = "Demo Parent", Phone = "+251 91 000 0000", Email = parent.Email };

        db.Centers.Add(center);
        db.Users.AddRange(admin, staff, parent);
        db.Classrooms.Add(room);
        db.Children.Add(child);
        db.Guardians.Add(guardian);
        db.SaveChanges();

        db.StaffProfiles.Add(new StaffProfile { UserId = staff.Id, JobTitle = "Lead teacher" });
        db.StaffClassrooms.Add(new StaffClassroom { UserId = staff.Id, ClassroomId = room.Id });
        db.ChildGuardians.Add(new ChildGuardian { ChildId = child.Id, GuardianId = guardian.Id, Relationship = "Mother", IsPrimary = true });
        db.SaveChanges();
    }
}
