using AppointmentSystem.API.Data;
using AppointmentSystem.API.DTOs;
using Microsoft.EntityFrameworkCore;

namespace AppointmentSystem.API.Services
{
    public class AppointmentService : IAppointmentService
    {
        private readonly AppointmentDbContext _context;
        private readonly IEmailService _emailService;

        public AppointmentService(AppointmentDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        public async Task<AppointmentResponse?> CreateAppointmentAsync(CreateAppointmentRequest request, string studentId)
        {
            // Validate advisor exists
            var advisor = await _context.Advisors.FindAsync(request.AdvisorId);
            if (advisor == null)
                return null;

            // Validate student exists
            var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == studentId);
            if (student == null)
                return null;

            // Check for conflicts
            var hasConflict = await _context.Appointments
                .AnyAsync(a => a.AdvisorId == request.AdvisorId &&
                              a.IsActive &&
                              a.Status != "Cancelled" &&
                              ((a.StartTime <= request.StartTime && a.EndTime > request.StartTime) ||
                               (a.StartTime < request.EndTime && a.EndTime >= request.EndTime) ||
                               (a.StartTime >= request.StartTime && a.EndTime <= request.EndTime)));

            if (hasConflict)
                return null;

            var appointment = new Models.Appointment
            {
                AdvisorId = request.AdvisorId,
                StudentId = student.Id,
                Subject = request.Subject,
                Description = request.Description,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                Status = "Scheduled",
                AppointmentType = request.AppointmentType
            };

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            var result = await GetAppointmentAsync(appointment.Id, studentId, "Student");
            
            // Send confirmation emails
            if (result != null)
            {
                try
                {
                    await _emailService.SendAppointmentConfirmationAsync(result, advisor.Email, $"{advisor.FirstName} {advisor.LastName}");
                    await _emailService.SendAppointmentConfirmationAsync(result, student.Email, $"{student.FirstName} {student.LastName}");
                }
                catch
                {
                    // Log error but don't fail appointment creation
                }
            }

            return result;
        }

        public async Task<AppointmentResponse?> UpdateAppointmentAsync(int appointmentId, UpdateAppointmentRequest request, string userId, string userRole)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Advisor)
                .Include(a => a.Student)
                .FirstOrDefaultAsync(a => a.Id == appointmentId && a.IsActive);

            if (appointment == null)
                return null;

            // Check permissions
            if (userRole == "Student" && appointment.Student.UserId != userId)
                return null;
            if (userRole == "Advisor" && appointment.Advisor.UserId != userId)
                return null;

            // Update fields
            if (!string.IsNullOrEmpty(request.Subject))
                appointment.Subject = request.Subject;
            if (request.Description != null)
                appointment.Description = request.Description;
            if (request.StartTime.HasValue)
                appointment.StartTime = request.StartTime.Value;
            if (request.EndTime.HasValue)
                appointment.EndTime = request.EndTime.Value;
            if (!string.IsNullOrEmpty(request.Status))
                appointment.Status = request.Status;
            if (!string.IsNullOrEmpty(request.AppointmentType))
                appointment.AppointmentType = request.AppointmentType;
            if (request.Notes != null)
                appointment.Notes = request.Notes;

            appointment.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return await GetAppointmentAsync(appointmentId, userId, userRole);
        }

        public async Task<bool> DeleteAppointmentAsync(int appointmentId, string userId, string userRole)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Student)
                .Include(a => a.Advisor)
                .FirstOrDefaultAsync(a => a.Id == appointmentId && a.IsActive);

            if (appointment == null)
                return false;

            // Check permissions
            if (userRole == "Student" && appointment.Student.UserId != userId)
                return false;
            if (userRole == "Advisor" && appointment.Advisor.UserId != userId)
                return false;

            // Soft delete - set IsActive to false instead of removing
            appointment.IsActive = false;
            appointment.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<AppointmentResponse?> GetAppointmentAsync(int appointmentId, string userId, string userRole)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Advisor)
                .Include(a => a.Student)
                .FirstOrDefaultAsync(a => a.Id == appointmentId && a.IsActive);

            if (appointment == null)
                return null;

            // Check permissions
            if (userRole == "Student" && appointment.Student.UserId != userId)
                return null;
            if (userRole == "Advisor" && appointment.Advisor.UserId != userId)
                return null;

            return new AppointmentResponse
            {
                Id = appointment.Id,
                AdvisorId = appointment.AdvisorId,
                AdvisorName = $"{appointment.Advisor.FirstName} {appointment.Advisor.LastName}",
                StudentId = appointment.StudentId,
                StudentName = $"{appointment.Student.FirstName} {appointment.Student.LastName}",
                Subject = appointment.Subject,
                Description = appointment.Description,
                StartTime = appointment.StartTime,
                EndTime = appointment.EndTime,
                Status = appointment.Status,
                AppointmentType = appointment.AppointmentType,
                Notes = appointment.Notes,
                CreatedAt = appointment.CreatedAt,
                UpdatedAt = appointment.UpdatedAt
            };
        }

        public async Task<IEnumerable<AppointmentResponse>> GetAppointmentsAsync(AppointmentListRequest request, string userId, string userRole)
        {
            var query = _context.Appointments
                .Include(a => a.Advisor)
                .Include(a => a.Student)
                .Where(a => a.IsActive) // Only show active appointments
                .AsQueryable();

            // Apply filters based on user role
            if (userRole == "Student")
            {
                var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == userId);
                if (student == null)
                    return Enumerable.Empty<AppointmentResponse>();

                query = query.Where(a => a.StudentId == student.Id);
            }
            else if (userRole == "Advisor")
            {
                var advisor = await _context.Advisors.FirstOrDefaultAsync(a => a.UserId == userId);
                if (advisor == null)
                    return Enumerable.Empty<AppointmentResponse>();

                query = query.Where(a => a.AdvisorId == advisor.Id);
            }

            // Apply additional filters
            if (request.AdvisorId.HasValue)
                query = query.Where(a => a.AdvisorId == request.AdvisorId.Value);
            if (request.StudentId.HasValue)
                query = query.Where(a => a.StudentId == request.StudentId.Value);
            if (!string.IsNullOrEmpty(request.Status))
                query = query.Where(a => a.Status == request.Status);
            if (request.StartDate.HasValue)
                query = query.Where(a => a.StartTime >= request.StartDate.Value);
            if (request.EndDate.HasValue)
                query = query.Where(a => a.StartTime <= request.EndDate.Value);

            // Apply pagination
            var appointments = await query
                .OrderBy(a => a.StartTime)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();

            return appointments.Select(a => new AppointmentResponse
            {
                Id = a.Id,
                AdvisorId = a.AdvisorId,
                AdvisorName = $"{a.Advisor.FirstName} {a.Advisor.LastName}",
                StudentId = a.StudentId,
                StudentName = $"{a.Student.FirstName} {a.Student.LastName}",
                Subject = a.Subject,
                Description = a.Description,
                StartTime = a.StartTime,
                EndTime = a.EndTime,
                Status = a.Status,
                AppointmentType = a.AppointmentType,
                Notes = a.Notes,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt
            });
        }

        public async Task<bool> CancelAppointmentAsync(int appointmentId, string userId, string userRole)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Student)
                .Include(a => a.Advisor)
                .FirstOrDefaultAsync(a => a.Id == appointmentId && a.IsActive);

            if (appointment == null)
                return false;

            // Check permissions
            if (userRole == "Student" && appointment.Student.UserId != userId)
                return false;
            if (userRole == "Advisor" && appointment.Advisor.UserId != userId)
                return false;

            appointment.Status = "Cancelled";
            appointment.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Send cancellation emails
            try
            {
                var appointmentResponse = await GetAppointmentAsync(appointmentId, userId, userRole);
                if (appointmentResponse != null)
                {
                    await _emailService.SendAppointmentCancellationAsync(appointmentResponse, appointment.Advisor.Email, $"{appointment.Advisor.FirstName} {appointment.Advisor.LastName}");
                    await _emailService.SendAppointmentCancellationAsync(appointmentResponse, appointment.Student.Email, $"{appointment.Student.FirstName} {appointment.Student.LastName}");
                }
            }
            catch
            {
                // Log error but don't fail cancellation
            }

            return true;
        }

        public async Task<bool> ConfirmAppointmentAsync(int appointmentId, string userId, string userRole)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Advisor)
                .FirstOrDefaultAsync(a => a.Id == appointmentId && a.IsActive);

            if (appointment == null)
                return false;

            // Only advisors can confirm appointments
            if (userRole != "Advisor" || appointment.Advisor.UserId != userId)
                return false;

            appointment.Status = "Confirmed";
            appointment.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Send confirmation emails
            try
            {
                var appointmentResponse = await GetAppointmentAsync(appointmentId, userId, userRole);
                if (appointmentResponse != null)
                {
                    await _emailService.SendAppointmentConfirmationAsync(appointmentResponse, appointment.Advisor.Email, $"{appointment.Advisor.FirstName} {appointment.Advisor.LastName}");
                    await _emailService.SendAppointmentConfirmationAsync(appointmentResponse, appointment.Student.Email, $"{appointment.Student.FirstName} {appointment.Student.LastName}");
                }
            }
            catch
            {
                // Log error but don't fail confirmation
            }

            return true;
        }
    }
}
