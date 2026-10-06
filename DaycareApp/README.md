# DaycareApp

React + Vite frontend, .NET 8 minimal API backend, PostgreSQL (external).

## Local development

### Prerequisites

- .NET 8 SDK
- Node.js and npm
- PostgreSQL running on `localhost:5432`

Copy `backend/DaycareApp.Api/appsettings.example.json` to
`backend/DaycareApp.Api/appsettings.json`, then set your local PostgreSQL password.
The local `appsettings.json` is ignored by Git because it contains credentials.
Create the `daycare` database if it does not already exist.

### First run

1. Install the EF CLI and create the initial migration once:
   ```sh
   dotnet tool install --global dotnet-ef
   cd backend/DaycareApp.Api
   dotnet ef migrations add Initial
   ```
2. Start the API in that directory:
   ```sh
   dotnet run --urls http://localhost:5000
   ```
   The API applies migrations and seeds demo data on startup.
3. In a second terminal, install and start the frontend:
   ```sh
   cd frontend
   npm install
   npm run dev
   ```
4. Open http://localhost:5173.

The frontend proxies `/api` requests to the API at `http://localhost:5000`.

For a non-default database or JWT secret, override the .NET configuration with
environment variables such as `ConnectionStrings__Default` and `Jwt__Key`.

Demo logins (when SEED_DEMO_DATA=true): admin@daycare.test / Admin123!  and  parent@daycare.test / Parent123!

Docker files are intentionally not part of the current local development workflow and can be added later.
