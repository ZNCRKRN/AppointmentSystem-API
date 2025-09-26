using AppointmentSystem.API.DTOs;

namespace AppointmentSystem.API.Services
{
    public interface IAuthService
    {
        Task<AuthResponse?> LoginAsync(LoginRequest request);
        Task<AuthResponse?> RegisterAsync(RegisterRequest request);
        Task<UserInfo?> GetUserInfoAsync(string userId);
    }
}

