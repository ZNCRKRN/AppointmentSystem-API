using System.ComponentModel.DataAnnotations;

namespace AppointmentSystem.API.Models
{
    public class Advisor
    {
        public int Id { get; set; }
        
        [Required]
        public string UserId { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(50)]
        public string LastName { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(100)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(100)]
        public string Department { get; set; } = string.Empty;
        
        [MaxLength(200)]
        public string? Specialization { get; set; }

        // A self-registered advisor starts unapproved and cannot log in until an admin approves
        // them; one created by seeding or by an admin is approved by default.
        public bool IsApproved { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
        public virtual ICollection<Availability> Availabilities { get; set; } = new List<Availability>();
    }
}

