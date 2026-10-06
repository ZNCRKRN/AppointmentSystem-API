using AppointmentSystem.API.Data;
using AppointmentSystem.API.DTOs;
using AppointmentSystem.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AppointmentSystem.API.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly IJwtService _jwtService;
        private readonly AppointmentDbContext _context;
        private readonly IEmailService _emailService;

        public AuthService(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            IJwtService jwtService,
            AppointmentDbContext context,
            IEmailService emailService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _jwtService = jwtService;
            _context = context;
            _emailService = emailService;
        }

        public async Task<AuthResponse?> LoginAsync(LoginRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
                return null;

            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
            if (!result.Succeeded)
                return null;

            var roles = await _userManager.GetRolesAsync(user);
            var token = _jwtService.GenerateToken(user.Id, user.Email!, roles);

            var userInfo = await GetUserInfoAsync(user.Id);
            if (userInfo == null)
                return null;

            return new AuthResponse
            {
                Token = token,
                Expiration = DateTime.UtcNow.AddHours(24),
                User = userInfo
            };
        }

        public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
        {
            var user = new IdentityUser
            {
                UserName = request.Email,
                Email = request.Email,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                return null;

            // Identity user and profile are separate writes; if either fails, remove the identity user
            // so a failed registration does not leave an account without a profile behind.
            try
            {
                var roleResult = await _userManager.AddToRoleAsync(user, request.Role);
                if (!roleResult.Succeeded)
                    throw new InvalidOperationException("Could not assign role");

                if (request.Role == "Student")
                {
                    _context.Students.Add(new Student
                    {
                        UserId = user.Id,
                        FirstName = request.FirstName,
                        LastName = request.LastName,
                        Email = request.Email,
                        StudentNumber = request.StudentNumber ?? "",
                        Department = request.Department ?? "",
                        Grade = request.Grade
                    });
                }
                else
                {
                    _context.Advisors.Add(new Advisor
                    {
                        UserId = user.Id,
                        FirstName = request.FirstName,
                        LastName = request.LastName,
                        Email = request.Email,
                        Department = request.Department ?? "",
                        Specialization = request.Specialization
                    });
                }

                await _context.SaveChangesAsync();
            }
            catch
            {
                // e.g. duplicate student number, or role assignment failure.
                // Drop the failed profile insert still pending in the change tracker, otherwise the
                // delete below would re-send it and fail again.
                _context.ChangeTracker.Clear();
                await _userManager.DeleteAsync(user);
                return null;
            }

            var roles = await _userManager.GetRolesAsync(user);
            var token = _jwtService.GenerateToken(user.Id, user.Email!, roles);

            var userInfo = await GetUserInfoAsync(user.Id);
            if (userInfo == null)
                return null;

            // Send welcome email
            try
            {
                await _emailService.SendWelcomeEmailAsync(user.Email!, $"{request.FirstName} {request.LastName}", request.Role);
            }
            catch
            {
                // Log error but don't fail registration
            }

            return new AuthResponse
            {
                Token = token,
                Expiration = DateTime.UtcNow.AddHours(24),
                User = userInfo
            };
        }

        public async Task ForgotPasswordAsync(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);

            // Callers get the same response whether or not the account exists, so this cannot be used to probe emails
            if (user == null)
                return;

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            try
            {
                await _emailService.SendPasswordResetEmailAsync(user.Email!, token);
            }
            catch
            {
                // Failure is logged by EmailService; the response stays generic
            }
        }

        public async Task<IdentityResult?> ResetPasswordAsync(ResetPasswordRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
                return null;

            return await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        }

        public async Task<UserInfo?> GetUserInfoAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return null;

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "";

            if (role == "Student")
            {
                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.UserId == userId);
                
                if (student == null)
                    return null;

                return new UserInfo
                {
                    Id = userId,
                    Email = user.Email!,
                    FirstName = student.FirstName,
                    LastName = student.LastName,
                    Role = role,
                    StudentNumber = student.StudentNumber,
                    Department = student.Department,
                    Grade = student.Grade
                };
            }
            else if (role == "Advisor")
            {
                var advisor = await _context.Advisors
                    .FirstOrDefaultAsync(a => a.UserId == userId);
                
                if (advisor == null)
                    return null;

                return new UserInfo
                {
                    Id = userId,
                    Email = user.Email!,
                    FirstName = advisor.FirstName,
                    LastName = advisor.LastName,
                    Role = role,
                    Department = advisor.Department,
                    Specialization = advisor.Specialization
                };
            }

            return new UserInfo
            {
                Id = userId,
                Email = user.Email!,
                FirstName = "",
                LastName = "",
                Role = role
            };
        }
    }
}
