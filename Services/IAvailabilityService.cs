using AppointmentSystem.API.DTOs;

namespace AppointmentSystem.API.Services
{
    public interface IAvailabilityService
    {
        Task<AvailabilityResponse?> CreateAvailabilityAsync(CreateAvailabilityRequest request, string advisorId);
        Task<AvailabilityResponse?> UpdateAvailabilityAsync(int availabilityId, UpdateAvailabilityRequest request, string advisorId);
        Task<bool> DeleteAvailabilityAsync(int availabilityId, string advisorId);
        Task<IEnumerable<AvailabilityResponse>> GetAvailabilitiesAsync(int advisorId);
        Task<IEnumerable<AvailableSlot>> GetAvailableSlotsAsync(AvailableSlotsRequest request);
    }
}

