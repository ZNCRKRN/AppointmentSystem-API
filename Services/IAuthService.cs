using AppointmentSystem.API.DTOs;
using Microsoft.AspNetCore.Identity;

namespace AppointmentSystem.API.Services
{
    public interface IAuthService
    {
        Task<AuthResponse?> LoginAsync(LoginRequest request);
        Task<AuthResponse?> RegisterAsync(RegisterRequest request);
        Task<UserInfo?> GetUserInfoAsync(string userId);
        Task ForgotPasswordAsync(string email);
        // Null when the account does not exist; otherwise the reset result with any validation errors
        Task<IdentityResult?> ResetPasswordAsync(ResetPasswordRequest request);
    }
}

