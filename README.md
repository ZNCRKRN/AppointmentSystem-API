# Appointment System API

ASP.NET Core 9 API for an academic appointment system: students book time with advisors, advisors manage their availability and confirm or complete appointments, and admins oversee users.

## Requirements

- .NET 9 SDK
- SQL Server LocalDB (installed with Visual Studio) or another SQL Server instance

## Setup

1. Set the JWT signing key in user-secrets (it is not stored in `appsettings.json`):

   ```bash
   dotnet user-secrets set "Jwt:SecretKey" "<at least 32 random characters>"
   ```

2. Set SMTP settings for email (reminders, confirmations, password reset):

   ```bash
   dotnet user-secrets set "Email:SmtpUsername" "<account>"
   dotnet user-secrets set "Email:SmtpPassword" "<app password>"
   ```

   `Email:SmtpServer`, `Email:FromEmail` and `FrontendUrl` are read from `appsettings.json`.

3. Apply the database migrations:

   ```bash
   dotnet ef database update
   ```

4. Run the API (listens on `http://localhost:5249` per `launchSettings.json`):

   ```bash
   dotnet run
   ```

Swagger is available at `/swagger` in the Development environment.

On startup the API seeds three demo accounts (created only if missing):

| Role    | Email                             | Password     |
|---------|-----------------------------------|--------------|
| Admin   | admin@appointmentsystem.com       | Admin123!    |
| Advisor | advisor@appointmentsystem.com     | Advisor123!  |
| Student | student@appointmentsystem.com     | Student123!  |

Change these before using the API anywhere other than a local machine.

## Main endpoints

| Area         | Endpoints |
|--------------|-----------|
| Auth         | `POST /api/Auth/login`, `POST /api/Auth/register` (Student or Advisor only), `GET /api/Auth/me`, `POST /api/Auth/forgot-password`, `POST /api/Auth/reset-password` |
| Appointments | `GET/POST /api/Appointment`, `GET/PUT/DELETE /api/Appointment/{id}`, `POST /api/Appointment/{id}/confirm`, `/cancel`, `/complete` |
| Availability | `GET/POST /api/Availability`, `PUT/DELETE /api/Availability/{id}`, `POST /api/Availability/available-slots` (advisor only, except slot lookup) |
| Users        | `GET /api/Advisor`, `GET /api/Advisor/my-profile`, `GET /api/Student` (Admin), `GET /api/Student/my-profile` |

Appointments are deleted with a soft delete (`IsActive = false`). Students can only book inside an advisor's availability, and bookings may not overlap an active appointment.

Reminder emails are sent for appointments starting within the next 24 hours; each appointment is reminded once. A background service checks every 15 minutes.

## Tests

```bash
cd tests/AppointmentSystem.API.Tests
dotnet test
```

The tests run the full API pipeline against an in-memory SQLite database. Emails are recorded, never sent, and no real secrets are needed.

## Migrations

Schema changes go through EF Core migrations:

```bash
dotnet ef migrations add <Name>
dotnet ef database update
```
