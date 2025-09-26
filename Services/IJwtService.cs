using System.Security.Claims;

namespace AppointmentSystem.API.Services
{
    public interface IJwtService
    {
        string GenerateToken(string userId, string email, IList<string> roles);
        ClaimsPrincipal? ValidateToken(string token);
    }
}

