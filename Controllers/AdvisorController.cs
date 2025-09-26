using AppointmentSystem.API.Data;
using AppointmentSystem.API.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AppointmentSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AdvisorController : ControllerBase
    {
        private readonly AppointmentDbContext _context;

        public AdvisorController(AppointmentDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetAdvisors()
        {
            var advisors = await _context.Advisors
                .Select(a => new
                {
                    a.Id,
                    a.FirstName,
                    a.LastName,
                    a.Email,
                    a.Department,
                    a.Specialization
                })
                .ToListAsync();

            return Ok(advisors);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetAdvisor(int id)
        {
            var advisor = await _context.Advisors
                .Where(a => a.Id == id)
                .Select(a => new
                {
                    a.Id,
                    a.FirstName,
                    a.LastName,
                    a.Email,
                    a.Department,
                    a.Specialization
                })
                .FirstOrDefaultAsync();

            if (advisor == null)
                return NotFound();

            return Ok(advisor);
        }

        [HttpGet("my-profile")]
        public async Task<ActionResult<object>> GetMyProfile()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Unauthorized();

            var advisor = await _context.Advisors
                .Where(a => a.UserId == userId)
                .Select(a => new
                {
                    a.Id,
                    a.FirstName,
                    a.LastName,
                    a.Email,
                    a.Department,
                    a.Specialization,
                    a.CreatedAt,
                    a.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (advisor == null)
                return NotFound("Advisor profile not found");

            return Ok(advisor);
        }
    }
}

