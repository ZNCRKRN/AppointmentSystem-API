using AppointmentSystem.API.DTOs;

namespace AppointmentSystem.API.Services
{
    public interface IEmailService
    {
        Task SendAppointmentConfirmationAsync(AppointmentResponse appointment, string recipientEmail, string recipientName);
        Task SendAppointmentReminderAsync(AppointmentResponse appointment, string recipientEmail, string recipientName);
        Task SendAppointmentCancellationAsync(AppointmentResponse appointment, string recipientEmail, string recipientName);
        Task SendWelcomeEmailAsync(string email, string name, string role);
        Task SendPasswordResetEmailAsync(string email, string resetToken);
    }
}

