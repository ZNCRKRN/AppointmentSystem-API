using AppointmentSystem.API.Data;
using AppointmentSystem.API.DTOs;
using AppointmentSystem.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AppointmentSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AvailabilityController : ControllerBase
    {
        private readonly IAvailabilityService _availabilityService;
        private readonly AppointmentDbContext _context;

        public AvailabilityController(IAvailabilityService availabilityService, AppointmentDbContext context)
        {
            _availabilityService = availabilityService;
            _context = context;
        }

        [HttpPost]
        public async Task<ActionResult<AvailabilityResponse>> CreateAvailability([FromBody] CreateAvailabilityRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Unauthorized();

            var result = await _availabilityService.CreateAvailabilityAsync(request, userId);
            if (result == null)
                return BadRequest("Failed to create availability. You must be an advisor.");

            return CreatedAtAction(nameof(GetAvailability), new { id = result.Id }, result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<AvailabilityResponse>> GetAvailability(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Unauthorized();

            var advisor = await _context.Advisors.FirstOrDefaultAsync(a => a.UserId == userId);
            if (advisor == null)
                return StatusCode(StatusCodes.Status403Forbidden, "Only advisors can access availability information");

            var availabilities = await _availabilityService.GetAvailabilitiesAsync(advisor.Id);
            var availability = availabilities.FirstOrDefault(a => a.Id == id);
            
            if (availability == null)
                return NotFound();

            return Ok(availability);
        }

        // Admins can view any advisor's schedule
        [HttpGet("advisor/{advisorId}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<AvailabilityResponse>>> GetAdvisorAvailabilities(int advisorId)
        {
            var availabilities = await _availabilityService.GetAvailabilitiesAsync(advisorId);
            return Ok(availabilities);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AvailabilityResponse>>> GetAvailabilities()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Unauthorized();

            var advisor = await _context.Advisors.FirstOrDefaultAsync(a => a.UserId == userId);
            if (advisor == null)
                return StatusCode(StatusCodes.Status403Forbidden, "Only advisors can access availability information");

            var availabilities = await _availabilityService.GetAvailabilitiesAsync(advisor.Id);
            return Ok(availabilities);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<AvailabilityResponse>> UpdateAvailability(int id, [FromBody] UpdateAvailabilityRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Unauthorized();

            var result = await _availabilityService.UpdateAvailabilityAsync(id, request, userId, User.IsInRole("Admin"));
            if (result == null)
                return NotFound("Availability not found or you don't have permission to update it");

            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteAvailability(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Unauthorized();

            var result = await _availabilityService.DeleteAvailabilityAsync(id, userId, User.IsInRole("Admin"));
            if (!result)
                return NotFound("Availability not found or you don't have permission to delete it");

            return NoContent();
        }

        [HttpPost("available-slots")]
        public async Task<ActionResult<IEnumerable<AvailableSlot>>> GetAvailableSlots([FromBody] AvailableSlotsRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var slots = await _availabilityService.GetAvailableSlotsAsync(request);
            return Ok(slots);
        }
    }
}

