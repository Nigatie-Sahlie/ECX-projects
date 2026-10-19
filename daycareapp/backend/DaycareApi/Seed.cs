namespace DaycareApi;

public static class Seed
{
    const string SuperAdminEmail = "super@admin.local";

    static string H(string p) => BCrypt.Net.BCrypt.HashPassword(p);

    public static void Run(AppDb db)
    {
        var superAdmin = db.Users.FirstOrDefault(u => u.Email == SuperAdminEmail)
            ?? db.Users.FirstOrDefault(u => u.Role == Roles.SuperAdmin);
        if (superAdmin is null)
        {
            superAdmin = new User
            {
                Email = SuperAdminEmail,
                Role = Roles.SuperAdmin
            };
            db.Users.Add(superAdmin);
        }

        superAdmin.PasswordHash = H("123456");
        superAdmin.FullName = "Platform Owner";

        // demo
        var center = db.Centers.FirstOrDefault(c => c.Name == "Sunshine Daycare (demo)");
        if (center is not null)
        {
            SyncDemoUser(db, center.Id, Roles.Admin, "admin@demo.local", "123456", "Demo Admin");
            SyncDemoUser(db, center.Id, Roles.Staff, "staff@demo.local", "123456", "Demo Teacher");
            SyncDemoUser(db, center.Id, Roles.Parent, "parent@demo.local", "123456", "Demo Parent");
            db.SaveChanges();
            return;
        }

        center = new Center { Name = "Sunshine Daycare (demo)", Address = "Bole, Addis Ababa", Phone = "+251 11 000 0000" };
        var admin = new User { CenterId = center.Id, Email = "admin@demo.local", PasswordHash = H("123456"), Role = Roles.Admin, FullName = "Demo Admin" };
        var staff = new User { CenterId = center.Id, Email = "staff@demo.local", PasswordHash = H("123456"), Role = Roles.Staff, FullName = "Demo Teacher" };
        var parent = new User { CenterId = center.Id, Email = "parent@demo.local", PasswordHash = H("123456"), Role = Roles.Parent, FullName = "Demo Parent" };
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

    static void SyncDemoUser(AppDb db, Guid centerId, string role, string email, string password, string fullName)
    {
        var user = db.Users.FirstOrDefault(u =>
            u.Email == email || (u.CenterId == centerId && u.Role == role));
        if (user is null)
        {
            user = new User { CenterId = centerId, Email = email, Role = role };
            db.Users.Add(user);
        }

        user.PasswordHash = H(password);
        user.FullName = fullName;
        user.IsActive = true;
    }
}


// "$HOME/bin/mailpit"     ---run in terminal to start mailpit server for testing email sending
// psql -h localhost -U postgres -d daycareapp  ----run in terminal to connect to database and check if email was sent
