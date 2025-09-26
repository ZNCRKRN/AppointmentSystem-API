using System.ComponentModel.DataAnnotations;

namespace AppointmentSystem.API.DTOs
{
    public class CreateAvailabilityRequest
    {
        [Required]
        public DayOfWeek DayOfWeek { get; set; }
        
        [Required]
        public TimeSpan StartTime { get; set; }
        
        [Required]
        public TimeSpan EndTime { get; set; }
        
        public bool IsRecurring { get; set; } = true;
        public DateTime? SpecificDate { get; set; }
    }

    public class UpdateAvailabilityRequest
    {
        public DayOfWeek? DayOfWeek { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public bool? IsRecurring { get; set; }
        public DateTime? SpecificDate { get; set; }
    }

    public class AvailabilityResponse
    {
        public int Id { get; set; }
        public int AdvisorId { get; set; }
        public string AdvisorName { get; set; } = string.Empty;
        public DayOfWeek DayOfWeek { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public bool IsRecurring { get; set; }
        public DateTime? SpecificDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class AvailableSlotsRequest
    {
        [Required]
        public int AdvisorId { get; set; }
        
        [Required]
        public DateTime Date { get; set; }
        
        public int DurationMinutes { get; set; } = 30;
    }

    public class AvailableSlot
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsAvailable { get; set; }
    }
}

