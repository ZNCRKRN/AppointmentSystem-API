using AppointmentSystem.API.DTOs;
using AppointmentSystem.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AppointmentSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AppointmentController : ControllerBase
    {
        private readonly IAppointmentService _appointmentService;

        public AppointmentController(IAppointmentService appointmentService)
        {
            _appointmentService = appointmentService;
        }

        [HttpPost]
        public async Task<ActionResult<AppointmentResponse>> CreateAppointment([FromBody] CreateAppointmentRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Unauthorized();

            var result = await _appointmentService.CreateAppointmentAsync(request, userId);
            if (result == null)
                return BadRequest("Failed to create appointment. Time slot may be unavailable.");

            return CreatedAtAction(nameof(GetAppointment), new { id = result.Id }, result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<AppointmentResponse>> GetAppointment(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            
            if (userId == null || userRole == null)
                return Unauthorized();

            var appointment = await _appointmentService.GetAppointmentAsync(id, userId, userRole);
            if (appointment == null)
                return NotFound();

            return Ok(appointment);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AppointmentResponse>>> GetAppointments([FromQuery] AppointmentListRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            
            if (userId == null || userRole == null)
                return Unauthorized();

            var appointments = await _appointmentService.GetAppointmentsAsync(request, userId, userRole);
            return Ok(appointments);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<AppointmentResponse>> UpdateAppointment(int id, [FromBody] UpdateAppointmentRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            
            if (userId == null || userRole == null)
                return Unauthorized();

            var result = await _appointmentService.UpdateAppointmentAsync(id, request, userId, userRole);
            if (result == null)
                return NotFound("Appointment not found or you don't have permission to update it");

            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteAppointment(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            
            if (userId == null || userRole == null)
                return Unauthorized();

            var result = await _appointmentService.DeleteAppointmentAsync(id, userId, userRole);
            if (!result)
                return NotFound("Appointment not found or you don't have permission to delete it");

            return NoContent();
        }

        [HttpPost("{id}/cancel")]
        public async Task<ActionResult> CancelAppointment(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            
            if (userId == null || userRole == null)
                return Unauthorized();

            var result = await _appointmentService.CancelAppointmentAsync(id, userId, userRole);
            if (!result)
                return NotFound("Appointment not found or you don't have permission to cancel it");

            return Ok(new { message = "Appointment cancelled successfully" });
        }

        [HttpPost("{id}/confirm")]
        public async Task<ActionResult> ConfirmAppointment(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            
            if (userId == null || userRole == null)
                return Unauthorized();

            var result = await _appointmentService.ConfirmAppointmentAsync(id, userId, userRole);
            if (!result)
                return NotFound("Appointment not found or you don't have permission to confirm it");

            return Ok(new { message = "Appointment confirmed successfully" });
        }
    }
}

