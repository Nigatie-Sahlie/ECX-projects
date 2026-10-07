# DaycarApp

Multi-daycare management app. **.NET 8 Web API + PostgreSQL + React (Vite).**

## What you need installed

1. .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
2. Node.js 20 or newer: https://nodejs.org
3. PostgreSQL 16: https://www.postgresql.org/download/ (remember the `postgres` user's password)

## Run it

**1. Database.** Edit `backend/DaycareApi/appsettings.json` and set your PostgreSQL password in `ConnectionStrings:Default`.
The database `daycareapp`, all tables and demo data are created automatically on first start.

**2. Backend** (http://localhost:5000)

```bash
cd backend/DaycareApi
dotnet run
```

**3. Frontend** (http://localhost:5173)

```bash
cd frontend
npm install
npm run dev
```

Open http://localhost:5173 and sign in.

## Demo accounts (created on first run)

| Role | Email | Password |
|---|---|---|
| Super admin (platform owner) | super@daycare.local | Super123! |
| Admin (Sunshine Daycare demo) | admin@demo.local | Admin123! |
| Staff / teacher | staff@demo.local | Staff123! |
| Parent | parent@demo.local | Parent123! |

Change these, and the `Jwt:Key` in `appsettings.json`, before real use.

## Who can do what

- **Super admin**: create, enable and disable daycares; creates each daycare's first admin.
- **Admin**: staff, classrooms, children, guardians (with optional parent logins), attendance, reports, announcements for their own daycare.
- **Staff**: only children in their assigned classrooms: check-in/out, daily reports, announcements to their classrooms.
- **Parent**: only their own children: shared daily reports, attendance history, announcements.

## How it maps to the schema

Tables: `Centers, Users, StaffProfiles, Classrooms, StaffClassrooms, Guardians, Children, ChildGuardians, Attendance, DailyReports, Entries (daily_report_entries), Announcements`.

- Daycare isolation: `AppDb.cs` adds a global query filter on `CenterId` taken from the login token, so one daycare can never read another's rows.
- Role rules per endpoint are in `Endpoints.cs`; the `Visible()` helper limits children by role (admin = all, staff = their classrooms, parent = linked children).
- Check constraint: only `super_admin` may have no `CenterId`.
- "Today" for attendance uses Ethiopia time (UTC+3), set by `EatHours` in `Endpoints.cs`.

## Before going to production

- Replace `EnsureCreated()` in `Program.cs` with EF Core migrations (`dotnet ef migrations add Init`).
- Use a real secret for `Jwt:Key` (environment variable), and serve over HTTPS.
- Not included yet: password reset, refresh tokens, child photos, payments, messaging.
- This project was written without being compiled or run in the authoring environment, so expect to fix a typo or two on first build.
