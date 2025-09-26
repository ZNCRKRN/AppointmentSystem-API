using AppointmentSystem.API.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AppointmentSystem.API.Data
{
    public class AppointmentDbContext : IdentityDbContext
    {
        public AppointmentDbContext(DbContextOptions<AppointmentDbContext> options) : base(options)
        {
        }

        public DbSet<Advisor> Advisors { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<Availability> Availabilities { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure Advisor entity
            builder.Entity<Advisor>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Department).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Specialization).HasMaxLength(200);
                entity.HasIndex(e => e.UserId).IsUnique();
            });

            // Configure Student entity
            builder.Entity<Student>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FirstName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.LastName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
                entity.Property(e => e.StudentNumber).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Department).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Grade).HasMaxLength(20);
                entity.HasIndex(e => e.UserId).IsUnique();
                entity.HasIndex(e => e.StudentNumber).IsUnique();
            });

            // Configure Appointment entity
            builder.Entity<Appointment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Subject).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
                entity.Property(e => e.AppointmentType).IsRequired().HasMaxLength(50);

                entity.HasOne(e => e.Advisor)
                    .WithMany(a => a.Appointments)
                    .HasForeignKey(e => e.AdvisorId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Student)
                    .WithMany(s => s.Appointments)
                    .HasForeignKey(e => e.StudentId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => new { e.AdvisorId, e.StartTime, e.EndTime });
            });

            // Configure Availability entity
            builder.Entity<Availability>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.DayOfWeek).IsRequired();
                entity.Property(e => e.IsRecurring).IsRequired();

                entity.HasOne(e => e.Advisor)
                    .WithMany(a => a.Availabilities)
                    .HasForeignKey(e => e.AdvisorId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.AdvisorId, e.DayOfWeek, e.StartTime });
            });
        }
    }
}

