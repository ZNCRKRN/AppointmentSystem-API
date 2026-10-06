using AppointmentSystem.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AppointmentSystem.API.Controllers
{
    public record UserRow(string Id, string Email, string Role, string FirstName, string LastName, string? Department);

    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly AppointmentDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public AdminController(AppointmentDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet("users")]
        public async Task<ActionResult<IEnumerable<UserRow>>> GetUsers()
        {
            var users = await _userManager.Users.ToListAsync();
            var students = await _context.Students.ToDictionaryAsync(s => s.UserId);
            var advisors = await _context.Advisors.ToDictionaryAsync(a => a.UserId);

            var result = new List<UserRow>();
            foreach (var user in users)
            {
                var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? "";
                students.TryGetValue(user.Id, out var student);
                advisors.TryGetValue(user.Id, out var advisor);

                result.Add(new UserRow(
                    user.Id,
                    user.Email ?? "",
                    role,
                    student?.FirstName ?? advisor?.FirstName ?? "",
                    student?.LastName ?? advisor?.LastName ?? "",
                    student?.Department ?? advisor?.Department));
            }

            return Ok(result.OrderBy(u => u.Role).ThenBy(u => u.Email));
        }

        // Deletes an account together with its profile, its appointments and (for advisors) its availability.
        [HttpDelete("users/{id}")]
        public async Task<ActionResult> DeleteUser(string id)
        {
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (id == currentUserId)
                return BadRequest("Kendi hesabınızı silemezsiniz.");

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound("Kullanıcı bulunamadı.");

            await using var transaction = await _context.Database.BeginTransactionAsync();

            // Appointments reference profiles with RESTRICT, so they must go first
            var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == id);
            if (student != null)
            {
                var studentAppointments = _context.Appointments.Where(a => a.StudentId == student.Id);
                _context.Appointments.RemoveRange(studentAppointments);
                _context.Students.Remove(student);
                await _context.SaveChangesAsync();
            }

            var advisor = await _context.Advisors.FirstOrDefaultAsync(a => a.UserId == id);
            if (advisor != null)
            {
                _context.Appointments.RemoveRange(_context.Appointments.Where(a => a.AdvisorId == advisor.Id));
                _context.Availabilities.RemoveRange(_context.Availabilities.Where(a => a.AdvisorId == advisor.Id));
                _context.Advisors.Remove(advisor);
                await _context.SaveChangesAsync();
            }

            // Clear tracked state so the identity delete below starts clean
            _context.ChangeTracker.Clear();

            var deleted = await _userManager.DeleteAsync(user);
            if (!deleted.Succeeded)
                return BadRequest(string.Join(" ", deleted.Errors.Select(e => e.Description)));

            await transaction.CommitAsync();
            return NoContent();
        }
    }
}
