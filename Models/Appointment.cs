using System.ComponentModel.DataAnnotations;

namespace AppointmentSystem.API.Models
{
    public class Appointment
    {
        public int Id { get; set; }
        
        [Required]
        public int AdvisorId { get; set; }
        
        [Required]
        public int StudentId { get; set; }
        
        [Required]
        [MaxLength(200)]
        public string Subject { get; set; } = string.Empty;
        
        [MaxLength(1000)]
        public string? Description { get; set; }
        
        [Required]
        public DateTime StartTime { get; set; }
        
        [Required]
        public DateTime EndTime { get; set; }
        
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Scheduled"; // Scheduled, Confirmed, Cancelled, Completed
        
        [Required]
        [MaxLength(50)]
        public string AppointmentType { get; set; } = "General"; // General, Academic, Career, Personal
        
        public string? Notes { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Soft delete property
        public bool IsActive { get; set; } = true;

        // Set once the reminder email has gone out, so each appointment is reminded only once
        public DateTime? ReminderSentAt { get; set; }
        
        // Navigation properties
        public virtual Advisor Advisor { get; set; } = null!;
        public virtual Student Student { get; set; } = null!;
    }
}

