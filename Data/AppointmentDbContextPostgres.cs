using Microsoft.EntityFrameworkCore;

namespace AppointmentSystem.API.Data
{
    /// <summary>
    /// Same model as <see cref="AppointmentDbContext"/>, but its own context type so it gets its own
    /// migrations set (EF Core ties each migration file to one context type). Local development keeps
    /// using SQL Server LocalDB and the migrations under Migrations/; a cloud deployment on PostgreSQL
    /// (no free managed SQL Server) uses this type and the migrations under Migrations/Postgres/.
    /// Selected in Program.cs via the Database:Provider setting.
    /// </summary>
    public class AppointmentDbContextPostgres : AppointmentDbContext
    {
        public AppointmentDbContextPostgres(DbContextOptions<AppointmentDbContextPostgres> options) : base(options)
        {
        }
    }
}
