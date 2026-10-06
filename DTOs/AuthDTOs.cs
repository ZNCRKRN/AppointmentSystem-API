using System.ComponentModel.DataAnnotations;

namespace AppointmentSystem.API.DTOs
{
    public class LoginRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        
        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterRequest : IValidatableObject
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        
        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;
        
        [Required]
        [MinLength(6)]
        [Compare(nameof(Password))]
        public string ConfirmPassword { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(50)]
        public string LastName { get; set; } = string.Empty;
        
        // Self-registration is limited to profile roles; Admin accounts are created by seeding only
        [Required]
        [RegularExpression("^(Student|Advisor)$", ErrorMessage = "Role must be Student or Advisor")]
        public string Role { get; set; } = string.Empty;

        // Student specific fields
        public string? StudentNumber { get; set; }
        public string? Department { get; set; }
        public string? Grade { get; set; }

        // Advisor specific fields
        public string? Specialization { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Role == "Student" && string.IsNullOrWhiteSpace(StudentNumber))
                yield return new ValidationResult("StudentNumber is required for students", new[] { nameof(StudentNumber) });
        }
    }

    public class ForgotPasswordRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public class ResetPasswordRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Token { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [Compare(nameof(NewPassword))]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class AuthResponse
    {
        public string Token { get; set; } = string.Empty;
        public DateTime Expiration { get; set; }
        public UserInfo User { get; set; } = new();
    }

    public class UserInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? StudentNumber { get; set; }
        public string? Department { get; set; }
        public string? Grade { get; set; }
        public string? Specialization { get; set; }
    }
}

