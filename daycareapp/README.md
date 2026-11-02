# KidSyncApp

Multi-daycare management app. **.NET 8 Web API + PostgreSQL + React (Vite).**

## What you need installed

1. .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
2. Node.js 20 or newer: https://nodejs.org
3. PostgreSQL 16: https://www.postgresql.org/download/ (remember the `postgres` user's password)
4. EF Core 8 CLI tool:

```bash
dotnet tool install --global dotnet-ef --version 8.*
```

## Run modes

The application supports three independent run modes. They use the same API and
frontend source, so messaging, notifications, password reset, and all other
features remain consistent. Each mode has its own database configuration and
should be tested separately.

### 1. Local backend and frontend

This mode uses PostgreSQL installed on the host. Configure the local database
connection and JWT key in a local copy of
`backend/DaycareApi/appsettings.example.json` named
`backend/DaycareApi/appsettings.json`. The database must exist before startup.

```bash
cd backend/DaycareApi
dotnet run
```

In another terminal:

```bash
cd frontend
npm install
npm run dev
```

Open http://localhost:5173. Development startup applies pending migrations and
seeds demo accounts. The API runs at http://localhost:5000.

### 2. Docker Compose development

This mode runs PostgreSQL, Mailpit, the API, and the frontend in containers.
Configure `POSTGRES_PASSWORD` and `JWT_KEY` in the root `.env` file.

```bash
docker compose up --build
```

Open http://localhost:5173. Stop it with `Ctrl+C`, or use:

```bash
docker compose down
```

The Docker development database is stored in the separate
`daycareapp_docker_postgres_data` volume and cannot be confused with the
production Compose database. Mailpit is available at http://localhost:8025.

### 3. Production Compose

Production does not seed demo data or apply migrations during API startup.
Create a separate `.env.production` file containing the production database
password, JWT key, public application origin, and optional bootstrap admin
values. Do not copy development credentials into it.

First apply database migrations:

```bash
docker compose --env-file .env.production -f docker-compose.prod.yml run --rm migrate
```

Start the production services:

```bash
docker compose --env-file .env.production -f docker-compose.prod.yml up --build -d
```

The application is available at the configured `APP_ORIGIN` (port 80 by
default). The production database is stored in the separate
`daycareapp_production_postgres_data` volume.

For a first deployment, create the initial super-admin once:

```bash
docker compose --env-file .env.production -f docker-compose.prod.yml \
  --profile setup run --rm bootstrap
```

Do not run the bootstrap command after a super-admin already exists.

### Test password reset locally

The development settings are configured for Mailpit, a local SMTP server that
captures email without sending it to real recipients.

1. Start Mailpit from the repository root:

   ```bash
      "$HOME/bin/mailpit"  
   ```

2. Start the backend and frontend using the commands above.
3. Open the Mailpit inbox at http://localhost:8025.
4. In the app, open **Forgot password?**, enter an existing demo account email,
   and submit the form.
5. Open the reset email in Mailpit, follow the link, choose a new password,
   and sign in with it.

The local SMTP settings use `localhost:1025` and are defined in the local
`backend/DaycareApi/appsettings.json`. Do not put real SMTP credentials in
the template or commit the local file. Docker Compose reads the corresponding
SMTP values from the ignored root `.env` file.

### Email and in-app notifications

New private messages and announcements create persistent in-app notifications.
Signed-in clients receive them immediately over the authenticated SignalR hub at
`/hubs/notifications`; the notification bell also loads recent notifications
from `/api/notifications` and supports marking them read. The same events are
sent by email through the configured SMTP server. SMTP can be disabled with
`Smtp:Enabled=false` without affecting in-app delivery.

## Demo accounts (created on first run)

| Role | Email | Password |
|---|---|---|
| Super admin (platform owner) | super@admin.local | 123456 |
| Admin (Sunshine Daycare demo) | admin@demo.local | 123456 |
| Staff / teacher | staff@demo.local | 123456 |
| Parent | parent@demo.local | 123456 |

Change these, and the `Jwt:Key` in `appsettings.json`, before real use.

## Who can do what

- **Super admin**: create, enable and disable daycares; creates each daycare's first admin.
- **Admin**: staff, classrooms, children, guardians (with optional parent logins), attendance, reports, announcements for their own daycare. Staff profiles can be updated or reassigned to classrooms; the children view supports server-side filtering by name, classroom, assigned staff, and active status.
- **Staff**: only children in their assigned classrooms: check-in/out, daily reports, announcements to their classrooms.
- **Parent**: only their own children: shared daily reports, attendance history, announcements.

## How it maps to the schema

Tables: `Centers, Users, StaffProfiles, Classrooms, StaffClassrooms, Guardians, Children, ChildGuardians, Attendance, DailyReports, Entries (daily_report_entries), Announcements, Conversations, Messages, Notifications`.

- Daycare isolation: `AppDb.cs` adds a global query filter on `CenterId` taken from the login token, so one daycare can never read another's rows.
- Role rules per endpoint are in `Endpoints.cs`; the `Visible()` helper limits children by role (admin = all, staff = their classrooms, parent = linked children).
- Check constraint: only `super_admin` may have no `CenterId`.
- "Today" for attendance uses Ethiopia time (UTC+3), set by `EatHours` in `Endpoints.cs`.

## EF Core migrations

The project uses the `Microsoft.EntityFrameworkCore.Design` 8.x package for migration
scaffolding. The `dotnet-ef` 8.x CLI tool is required for the commands below. The design
package is marked as a private development dependency and is not included in the deployed
application.

From `backend/DaycareApi`:

```bash
# Create a migration after changing Models.cs or AppDb.cs
dotnet ef migrations add <MigrationName>

# Apply pending migrations to the configured database
dotnet ef database update
```

Development startup runs `Database.Migrate()` before `Seed.Run(db)`. Production startup
does not apply migrations automatically; run `dotnet ef database update` as part of the
deployment process.

### Permission integration tests

The backend includes a small xUnit integration suite using a separate PostgreSQL database.
It refuses to run if the configured database is named `daycareapp`; the test database is
never dropped or cleared.

Create an empty database such as `daycareapp_test`, then run:

```bash
cd backend
export DAYCARE_TEST_CONNECTION='Host=localhost;Port=5432;Database=daycareapp_test;Username=postgres;Password=...'
dotnet test DaycareApi.Tests/DaycareApi.Tests.csproj
```

The test host creates the schema with `EnsureCreated`, starts the API in the `Testing`
environment, and uses the seeded demo accounts to verify authentication and role-based
endpoint permissions. Password-reset tests create isolated users and replace SMTP with
an in-memory email sender, so they never send real email. Keep
`DAYCARE_TEST_CONNECTION` pointed at a disposable test database; no production code
path is changed by the test host.

### Moving a database created with EnsureCreated

`EnsureCreated()` does not create the `__EFMigrationsHistory` table, so an existing database
created that way cannot safely be treated as an already-migrated database. Do not start the
updated application against that database until it has been migrated or recreated.

For this demo database, the safest option is to stop the API, drop only the `daycareapp`
database, recreate an empty database with the same name, and then run the first migration
with `dotnet ef database update` (or start the API in development). This removes the demo
data, so confirm that the database may be destroyed before doing it. If the data must be
preserved, take a backup and use a deliberate baseline/data-migration plan instead of
dropping it.

## Before going to production

- Use a real secret for `Jwt:Key` (environment variable), and serve over HTTPS.
- Configure `Smtp` and `Frontend:ResetPasswordUrl` in `appsettings.json` for forgot-password emails.
- Payments are intentionally deferred.

## Authentication and modular structure****

Authentication endpoints are grouped in `AuthEndpoints.cs`. The forgot-password flow
stores only a hash of a short-lived, single-use reset token. Requests return the same
message whether or not an email exists, preventing account enumeration. Configure SMTP
with environment-specific settings or a secret manager; do not commit SMTP passwords.

Example local configuration shape:

```json
{
  "Frontend": {
    "ResetPasswordUrl": "http://localhost:5173/reset-password"
  },
  "Smtp": {
    "Enabled": true,
    "Host": "smtp.example.com",
    "Port": 587,
    "UseSsl": true,
    "From": "no-reply@example.com",
    "Username": "smtp-user",
    "Password": "set-this-outside-source-control"
  }
}
```

The backend keeps authentication infrastructure (`AuthEndpoints.cs`, `Infrastructure.cs`)
separate from the feature endpoint mappings in `Endpoints.cs`. `ApiExceptionMiddleware`
provides a single safe error response for unexpected API failures while logging the
diagnostic server-side. New feature modules should follow the same pattern: focused
endpoint mapping, request records, services, and tests rather than growing one handler.
