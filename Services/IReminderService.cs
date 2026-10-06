namespace AppointmentSystem.API.Services
{
    public interface IReminderService
    {
        // Sends reminders for active appointments starting within the next 24 hours; returns how many were processed
        Task<int> SendDueRemindersAsync(DateTime now);
    }
}
