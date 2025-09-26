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

            await _userManager.AddToRoleAsync(user, request.Role);

            // Create profile based on role
            if (request.Role == "Student")
            {
                var student = new Student
                {
                    UserId = user.Id,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Email = request.Email,
                    StudentNumber = request.StudentNumber ?? "",
                    Department = request.Department ?? "",
                    Grade = request.Grade
                };
                _context.Students.Add(student);
            }
            else if (request.Role == "Advisor")
            {
                var advisor = new Advisor
                {
                    UserId = user.Id,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    Email = request.Email,
                    Department = request.Department ?? "",
                    Specialization = request.Specialization
                };
                _context.Advisors.Add(advisor);
            }

            await _context.SaveChangesAsync();

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
