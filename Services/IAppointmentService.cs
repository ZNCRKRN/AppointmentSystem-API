using AppointmentSystem.API.DTOs;

namespace AppointmentSystem.API.Services
{
    public interface IAppointmentService
    {
        Task<AppointmentResponse?> CreateAppointmentAsync(CreateAppointmentRequest request, string studentId);
        Task<AppointmentResponse?> UpdateAppointmentAsync(int appointmentId, UpdateAppointmentRequest request, string userId, string userRole);
        Task<bool> DeleteAppointmentAsync(int appointmentId, string userId, string userRole);
        Task<AppointmentResponse?> GetAppointmentAsync(int appointmentId, string userId, string userRole);
        Task<IEnumerable<AppointmentResponse>> GetAppointmentsAsync(AppointmentListRequest request, string userId, string userRole);
        Task<bool> CancelAppointmentAsync(int appointmentId, string userId, string userRole);
        Task<bool> ConfirmAppointmentAsync(int appointmentId, string userId, string userRole);
    }
}

