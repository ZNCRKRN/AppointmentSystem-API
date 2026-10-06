using AppointmentSystem.API.Data;
using AppointmentSystem.API.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AppointmentSystem.API.Services
{
    public class AvailabilityService : IAvailabilityService
    {
        private readonly AppointmentDbContext _context;

        public AvailabilityService(AppointmentDbContext context)
        {
            _context = context;
        }

        public async Task<AvailabilityResponse?> CreateAvailabilityAsync(CreateAvailabilityRequest request, string advisorId)
        {
            var advisor = await _context.Advisors.FirstOrDefaultAsync(a => a.UserId == advisorId);
            if (advisor == null)
                return null;

            var availability = new Models.Availability
            {
                AdvisorId = advisor.Id,
                DayOfWeek = request.DayOfWeek,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                IsRecurring = request.IsRecurring,
                SpecificDate = request.SpecificDate
            };

            _context.Availabilities.Add(availability);
            await _context.SaveChangesAsync();

            return new AvailabilityResponse
            {
                Id = availability.Id,
                AdvisorId = availability.AdvisorId,
                AdvisorName = $"{advisor.FirstName} {advisor.LastName}",
                DayOfWeek = availability.DayOfWeek,
                StartTime = availability.StartTime,
                EndTime = availability.EndTime,
                IsRecurring = availability.IsRecurring,
                SpecificDate = availability.SpecificDate,
                CreatedAt = availability.CreatedAt,
                UpdatedAt = availability.UpdatedAt
            };
        }

        public async Task<AvailabilityResponse?> UpdateAvailabilityAsync(int availabilityId, UpdateAvailabilityRequest request, string advisorId, bool isAdmin = false)
        {
            var availability = await _context.Availabilities
                .Include(a => a.Advisor)
                .FirstOrDefaultAsync(a => a.Id == availabilityId && (isAdmin || a.Advisor.UserId == advisorId));

            if (availability == null)
                return null;

            if (request.DayOfWeek.HasValue)
                availability.DayOfWeek = request.DayOfWeek.Value;
            if (request.StartTime.HasValue)
                availability.StartTime = request.StartTime.Value;
            if (request.EndTime.HasValue)
                availability.EndTime = request.EndTime.Value;
            if (request.IsRecurring.HasValue)
                availability.IsRecurring = request.IsRecurring.Value;
            if (request.SpecificDate.HasValue)
                availability.SpecificDate = request.SpecificDate.Value;

            availability.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return new AvailabilityResponse
            {
                Id = availability.Id,
                AdvisorId = availability.AdvisorId,
                AdvisorName = $"{availability.Advisor.FirstName} {availability.Advisor.LastName}",
                DayOfWeek = availability.DayOfWeek,
                StartTime = availability.StartTime,
                EndTime = availability.EndTime,
                IsRecurring = availability.IsRecurring,
                SpecificDate = availability.SpecificDate,
                CreatedAt = availability.CreatedAt,
                UpdatedAt = availability.UpdatedAt
            };
        }

        public async Task<bool> DeleteAvailabilityAsync(int availabilityId, string advisorId, bool isAdmin = false)
        {
            var availability = await _context.Availabilities
                .Include(a => a.Advisor)
                .FirstOrDefaultAsync(a => a.Id == availabilityId && (isAdmin || a.Advisor.UserId == advisorId));

            if (availability == null)
                return false;

            _context.Availabilities.Remove(availability);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<IEnumerable<AvailabilityResponse>> GetAvailabilitiesAsync(int advisorId)
        {
            var availabilities = await _context.Availabilities
                .Include(a => a.Advisor)
                .Where(a => a.AdvisorId == advisorId)
                .ToListAsync();

            // Sorted in memory: SQLite cannot ORDER BY TimeSpan columns
            availabilities = availabilities
                .OrderBy(a => a.DayOfWeek)
                .ThenBy(a => a.StartTime)
                .ToList();

            return availabilities.Select(a => new AvailabilityResponse
            {
                Id = a.Id,
                AdvisorId = a.AdvisorId,
                AdvisorName = $"{a.Advisor.FirstName} {a.Advisor.LastName}",
                DayOfWeek = a.DayOfWeek,
                StartTime = a.StartTime,
                EndTime = a.EndTime,
                IsRecurring = a.IsRecurring,
                SpecificDate = a.SpecificDate,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt
            });
        }

        public async Task<IEnumerable<AvailableSlot>> GetAvailableSlotsAsync(AvailableSlotsRequest request)
        {
            var advisor = await _context.Advisors.FindAsync(request.AdvisorId);
            if (advisor == null)
                return Enumerable.Empty<AvailableSlot>();

            var dayOfWeek = request.Date.DayOfWeek;
            var slots = new List<AvailableSlot>();

            // Get recurring availabilities for this day of week
            var recurringAvailabilities = await _context.Availabilities
                .Where(a => a.AdvisorId == request.AdvisorId && 
                           a.DayOfWeek == dayOfWeek && 
                           a.IsRecurring)
                .ToListAsync();

            // Get specific date availabilities
            var specificAvailabilities = await _context.Availabilities
                .Where(a => a.AdvisorId == request.AdvisorId &&
                           !a.IsRecurring &&
                           a.SpecificDate.HasValue &&
                           a.SpecificDate.Value.Date == request.Date.Date)
                .ToListAsync();

            // Get existing appointments for this date (soft-deleted ones must not block slots)
            var existingAppointments = await _context.Appointments
                .Where(a => a.AdvisorId == request.AdvisorId &&
                           a.IsActive &&
                           a.StartTime.Date == request.Date.Date &&
                           a.Status != "Cancelled")
                .ToListAsync();

            var now = DateTime.Now;

            // Combine all availabilities
            var allAvailabilities = recurringAvailabilities.Concat(specificAvailabilities);

            foreach (var availability in allAvailabilities)
            {
                var startDateTime = request.Date.Date.Add(availability.StartTime);
                var endDateTime = request.Date.Date.Add(availability.EndTime);

                var currentTime = startDateTime;
                while (currentTime.AddMinutes(request.DurationMinutes) <= endDateTime)
                {
                    var slotEndTime = currentTime.AddMinutes(request.DurationMinutes);

                    // Past slots are not bookable, so they are not offered
                    if (currentTime < now)
                    {
                        currentTime = currentTime.AddMinutes(15);
                        continue;
                    }

                    // Check if this slot conflicts with existing appointments
                    var hasConflict = existingAppointments.Any(apt => 
                        (apt.StartTime < slotEndTime && apt.EndTime > currentTime));

                    slots.Add(new AvailableSlot
                    {
                        StartTime = currentTime,
                        EndTime = slotEndTime,
                        IsAvailable = !hasConflict
                    });

                    currentTime = currentTime.AddMinutes(15); // 15-minute intervals
                }
            }

            return slots.OrderBy(s => s.StartTime);
        }
    }
}

