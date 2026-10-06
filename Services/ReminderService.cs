using AppointmentSystem.API.Data;
using AppointmentSystem.API.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AppointmentSystem.API.Services
{
    public class ReminderService : IReminderService
    {
        private static readonly TimeSpan ReminderWindow = TimeSpan.FromHours(24);

        private readonly AppointmentDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<ReminderService> _logger;

        public ReminderService(AppointmentDbContext context, IEmailService emailService, ILogger<ReminderService> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<int> SendDueRemindersAsync(DateTime now)
        {
            var windowEnd = now.Add(ReminderWindow);

            var due = await _context.Appointments
                .Include(a => a.Advisor)
                .Include(a => a.Student)
                .Where(a => a.IsActive &&
                            a.ReminderSentAt == null &&
                            (a.Status == "Scheduled" || a.Status == "Confirmed") &&
                            a.StartTime > now &&
                            a.StartTime <= windowEnd)
                .ToListAsync();

            foreach (var appointment in due)
            {
                var response = new AppointmentResponse
                {
                    Id = appointment.Id,
                    AdvisorId = appointment.AdvisorId,
                    AdvisorName = $"{appointment.Advisor.FirstName} {appointment.Advisor.LastName}",
                    StudentId = appointment.StudentId,
                    StudentName = $"{appointment.Student.FirstName} {appointment.Student.LastName}",
                    Subject = appointment.Subject,
                    Description = appointment.Description,
                    StartTime = appointment.StartTime,
                    EndTime = appointment.EndTime,
                    Status = appointment.Status,
                    AppointmentType = appointment.AppointmentType,
                    Notes = appointment.Notes,
                    CreatedAt = appointment.CreatedAt,
                    UpdatedAt = appointment.UpdatedAt
                };

                // Each send is attempted separately; the reminder is marked either way so a failing
                // mail server cannot cause the same appointment to be reminded over and over.
                await TrySendAsync(response, appointment.Advisor.Email, response.AdvisorName, appointment.Id);
                await TrySendAsync(response, appointment.Student.Email, response.StudentName, appointment.Id);

                appointment.ReminderSentAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return due.Count;
        }

        private async Task TrySendAsync(AppointmentResponse response, string email, string name, int appointmentId)
        {
            try
            {
                await _emailService.SendAppointmentReminderAsync(response, email, name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reminder email failed for appointment {AppointmentId}", appointmentId);
            }
        }
    }
}
