using AppointmentSystem.API.DTOs;

namespace AppointmentSystem.API.Services
{
    public interface IAvailabilityService
    {
        Task<AvailabilityResponse?> CreateAvailabilityAsync(CreateAvailabilityRequest request, string advisorId);
        // For an Admin creating availability on behalf of an advisor, identified by Advisor.Id rather than the advisor's own user id
        Task<AvailabilityResponse?> CreateAvailabilityForAdvisorAsync(CreateAvailabilityRequest request, int advisorId);
        Task<AvailabilityResponse?> UpdateAvailabilityAsync(int availabilityId, UpdateAvailabilityRequest request, string advisorId, bool isAdmin = false);
        Task<bool> DeleteAvailabilityAsync(int availabilityId, string advisorId, bool isAdmin = false);
        Task<IEnumerable<AvailabilityResponse>> GetAvailabilitiesAsync(int advisorId);
        Task<IEnumerable<AvailableSlot>> GetAvailableSlotsAsync(AvailableSlotsRequest request);
    }
}

