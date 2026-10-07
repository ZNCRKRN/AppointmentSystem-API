using System.ComponentModel.DataAnnotations;

namespace AppointmentSystem.API.DTOs
{
    public class CreateAvailabilityRequest : IValidatableObject
    {
        [Required]
        public DayOfWeek DayOfWeek { get; set; }

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }

        public bool IsRecurring { get; set; } = true;
        public DateTime? SpecificDate { get; set; }

        // Set by an Admin to create availability on behalf of a specific advisor; ignored otherwise
        public int? AdvisorId { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (EndTime <= StartTime)
                yield return new ValidationResult("EndTime must be after StartTime", new[] { nameof(EndTime) });

            if (!IsRecurring && SpecificDate == null)
                yield return new ValidationResult("SpecificDate is required for one-time availability", new[] { nameof(SpecificDate) });
        }
    }

    public class UpdateAvailabilityRequest : IValidatableObject
    {
        public DayOfWeek? DayOfWeek { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public bool? IsRecurring { get; set; }
        public DateTime? SpecificDate { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (StartTime.HasValue && EndTime.HasValue && EndTime.Value <= StartTime.Value)
                yield return new ValidationResult("EndTime must be after StartTime", new[] { nameof(EndTime) });
        }
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

        // Lower bound prevents an endless loop in slot generation when the value is zero or negative
        [Range(5, 480)]
        public int DurationMinutes { get; set; } = 30;
    }

    public class AvailableSlot
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsAvailable { get; set; }
    }
}

