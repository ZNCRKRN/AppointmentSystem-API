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

            // Students may only book inside the advisor's availability
            if (!await IsWithinAvailabilityAsync(request.AdvisorId, request.StartTime, request.EndTime))
                return null;

            if (await HasConflictAsync(request.AdvisorId, request.StartTime, request.EndTime, excludeAppointmentId: null))
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
            
            // Request emails: the advisor must still confirm, so nothing is "confirmed" yet
            if (result != null)
            {
                try
                {
                    await _emailService.SendAppointmentRequestedAsync(result, advisor.Email, $"{advisor.FirstName} {advisor.LastName}", isAdvisorRecipient: true);
                    await _emailService.SendAppointmentRequestedAsync(result, student.Email, $"{student.FirstName} {student.LastName}", isAdvisorRecipient: false);
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

            var newStart = request.StartTime ?? appointment.StartTime;
            var newEnd = request.EndTime ?? appointment.EndTime;

            // Rescheduling must not overlap another active appointment of the same advisor
            if (newStart != appointment.StartTime || newEnd != appointment.EndTime)
            {
                if (!await IsWithinAvailabilityAsync(appointment.AdvisorId, newStart, newEnd))
                    return null;
                if (await HasConflictAsync(appointment.AdvisorId, newStart, newEnd, excludeAppointmentId: appointment.Id))
                    return null;
            }

            // Update fields
            if (!string.IsNullOrEmpty(request.Subject))
                appointment.Subject = request.Subject;
            if (request.Description != null)
                appointment.Description = request.Description;
            appointment.StartTime = newStart;
            appointment.EndTime = newEnd;
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

            // Apply pagination (bounded so a client cannot request an unbounded page)
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);

            var appointments = await query
                .OrderBy(a => a.StartTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
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

            // A finished appointment is history and cannot be cancelled
            if (appointment.Status == "Completed")
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

            // Only advisors can confirm appointments, and only ones still waiting for a decision
            if (!CanManageAsAdvisor(appointment, userId, userRole))
                return false;
            if (appointment.Status != "Scheduled")
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

        public async Task<bool> CompleteAppointmentAsync(int appointmentId, string userId, string userRole)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Advisor)
                .FirstOrDefaultAsync(a => a.Id == appointmentId && a.IsActive);

            if (appointment == null)
                return false;

            // Only the advisor of a confirmed appointment can mark it as completed
            if (!CanManageAsAdvisor(appointment, userId, userRole))
                return false;
            if (appointment.Status != "Confirmed")
                return false;

            appointment.Status = "Completed";
            appointment.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return true;
        }

        // Admins can act on any appointment; advisors only on their own
        private static bool CanManageAsAdvisor(Models.Appointment appointment, string userId, string userRole) =>
            userRole == "Admin" || (userRole == "Advisor" && appointment.Advisor.UserId == userId);

        // The whole slot must sit inside one availability window (recurring weekday or one-time date)
        private async Task<bool> IsWithinAvailabilityAsync(int advisorId, DateTime start, DateTime end)
        {
            if (end.Date != start.Date)
                return false;

            var day = start.DayOfWeek;
            var date = start.Date;
            var from = start.TimeOfDay;
            var to = end.TimeOfDay;

            // Filtered in memory: an advisor has few availability rows, and TimeSpan comparisons
            // do not translate to every provider (e.g. SQLite)
            var windows = await _context.Availabilities
                .Where(a => a.AdvisorId == advisorId)
                .ToListAsync();

            return windows.Any(a =>
                ((a.IsRecurring && a.DayOfWeek == day) ||
                 (!a.IsRecurring && a.SpecificDate.HasValue && a.SpecificDate.Value.Date == date)) &&
                a.StartTime <= from && a.EndTime >= to);
        }

        private async Task<bool> HasConflictAsync(int advisorId, DateTime start, DateTime end, int? excludeAppointmentId)
        {
            return await _context.Appointments
                .AnyAsync(a => a.AdvisorId == advisorId &&
                              a.IsActive &&
                              a.Status != "Cancelled" &&
                              (excludeAppointmentId == null || a.Id != excludeAppointmentId) &&
                              a.StartTime < end &&
                              a.EndTime > start);
        }
    }
}
