using AppointmentSystem.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AppointmentSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class StudentController : ControllerBase
    {
        private readonly AppointmentDbContext _context;

        public StudentController(AppointmentDbContext context)
        {
            _context = context;
        }

        [HttpGet("my-profile")]
        public async Task<ActionResult<object>> GetMyProfile()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Unauthorized();

            var student = await _context.Students
                .Where(s => s.UserId == userId)
                .Select(s => new
                {
                    s.Id,
                    s.FirstName,
                    s.LastName,
                    s.Email,
                    s.StudentNumber,
                    s.Department,
                    s.Grade,
                    s.CreatedAt,
                    s.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (student == null)
                return NotFound("Student profile not found");

            return Ok(student);
        }
    }
}

