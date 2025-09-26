using System.ComponentModel.DataAnnotations;

namespace AppointmentSystem.API.Models
{
    public class Availability
    {
        public int Id { get; set; }
        
        [Required]
        public int AdvisorId { get; set; }
        
        [Required]
        public DayOfWeek DayOfWeek { get; set; }
        
        [Required]
        public TimeSpan StartTime { get; set; }
        
        [Required]
        public TimeSpan EndTime { get; set; }
        
        [Required]
        public bool IsRecurring { get; set; } = true;
        
        public DateTime? SpecificDate { get; set; } // For non-recurring availability
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public virtual Advisor Advisor { get; set; } = null!;
    }
}

