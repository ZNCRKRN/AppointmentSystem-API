using AppointmentSystem.API.DTOs;
using AppointmentSystem.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AppointmentSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.LoginAsync(request);
            if (result == null)
                return Unauthorized("Invalid email or password");

            return Ok(result);
        }

        [HttpPost("register")]
        public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.RegisterAsync(request);
            if (result == null)
                return BadRequest("Registration failed. Email may already exist.");

            return Ok(result);
        }

        [HttpPost("forgot-password")]
        public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            await _authService.ForgotPasswordAsync(request.Email);

            // Same answer for known and unknown emails
            return Ok(new { message = "If an account exists for this email, a reset link has been sent." });
        }

        [HttpPost("reset-password")]
        public async Task<ActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _authService.ResetPasswordAsync(request);
            if (result == null)
                return BadRequest("Bağlantı geçersiz veya süresi dolmuş.");

            if (!result.Succeeded)
            {
                // Identity reports a bad or expired token with the InvalidToken code; anything else is a password rule
                if (result.Errors.Any(e => e.Code == "InvalidToken"))
                    return BadRequest("Bağlantı geçersiz veya süresi dolmuş.");

                return BadRequest("Şifre gereksinimleri karşılanmıyor: " + string.Join(" ", result.Errors.Select(e => e.Description)));
            }

            return Ok(new { message = "Password has been reset. You can now log in." });
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<UserInfo>> GetCurrentUser()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Unauthorized();

            var userInfo = await _authService.GetUserInfoAsync(userId);
            if (userInfo == null)
                return NotFound("User not found");

            return Ok(userInfo);
        }
    }
}

