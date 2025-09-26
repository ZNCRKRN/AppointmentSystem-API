using System.ComponentModel.DataAnnotations;

namespace AppointmentSystem.API.DTOs
{
    public class CreateAppointmentRequest
    {
        [Required]
        public int AdvisorId { get; set; }
        
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
        [MaxLength(50)]
        public string AppointmentType { get; set; } = "General";
    }

    public class UpdateAppointmentRequest
    {
        [MaxLength(200)]
        public string? Subject { get; set; }
        
        [MaxLength(1000)]
        public string? Description { get; set; }
        
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        
        [MaxLength(20)]
        public string? Status { get; set; }
        
        [MaxLength(50)]
        public string? AppointmentType { get; set; }
        
        public string? Notes { get; set; }
    }

    public class AppointmentResponse
    {
        public int Id { get; set; }
        public int AdvisorId { get; set; }
        public string AdvisorName { get; set; } = string.Empty;
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public string AppointmentType { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class AppointmentListRequest
    {
        public int? AdvisorId { get; set; }
        public int? StudentId { get; set; }
        public string? Status { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}

