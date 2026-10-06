using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AppointmentSystem.API.Data;
using AppointmentSystem.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AppointmentSystem.API.Tests;

public class ReminderTests
{
    private static async Task OpenAllWeekAsync(HttpClient client, string advisorToken)
    {
        ApiFactory.Authenticate(client, advisorToken);
        for (var day = 0; day < 7; day++)
        {
            (await client.PostAsJsonAsync("/api/Availability", new
            {
                dayOfWeek = day,
                startTime = "00:00:00",
                endTime = "23:59:00",
                isRecurring = true
            })).EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task Reminder_IsSentOnceToStudentAndAdvisor_ForAppointmentsInNext24Hours()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();

        var advisorToken = await ApiFactory.LoginAsync(client, "advisor@appointmentsystem.com", "Advisor123!");
        var studentToken = await ApiFactory.LoginAsync(client, "student@appointmentsystem.com", "Student123!");
        ApiFactory.Authenticate(client, advisorToken);
        var advisorId = await ApiFactory.GetProfileIdAsync(client, "/api/Advisor/my-profile");
        await OpenAllWeekAsync(client, advisorToken);

        // Booked far ahead, then moved into the reminder window directly in the database
        // (moving it through the API would require availability for that exact time of day)
        ApiFactory.Authenticate(client, studentToken);
        var created = await client.PostAsJsonAsync("/api/Appointment", new
        {
            advisorId,
            subject = "Soon",
            startTime = DateTime.Today.AddDays(7).AddHours(10),
            endTime = DateTime.Today.AddDays(7).AddHours(10).AddMinutes(30),
            appointmentType = "General"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var soonId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var now = DateTime.Now;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppointmentDbContext>();
            var appointment = await db.Appointments.FirstAsync(a => a.Id == soonId);
            appointment.StartTime = now.AddHours(2);
            appointment.EndTime = now.AddHours(2).AddMinutes(30);
            await db.SaveChangesAsync();
        }

        var reminders = factory.Services.CreateScope().ServiceProvider.GetRequiredService<IReminderService>();
        var first = await reminders.SendDueRemindersAsync(DateTime.Now);
        Assert.Equal(1, first);

        var sentReminders = factory.Emails.Sent.Where(s => s.Kind == "reminder").Select(s => s.Recipient).ToList();
        Assert.Contains("advisor@appointmentsystem.com", sentReminders);
        Assert.Contains("student@appointmentsystem.com", sentReminders);

        // Second pass must not remind the same appointment again
        var second = await reminders.SendDueRemindersAsync(DateTime.Now);
        Assert.Equal(0, second);
        Assert.Equal(2, factory.Emails.Sent.Count(s => s.Kind == "reminder"));
    }

    [Fact]
    public async Task Reminder_IgnoresAppointmentsBeyondTheWindow()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();

        var advisorToken = await ApiFactory.LoginAsync(client, "advisor@appointmentsystem.com", "Advisor123!");
        var studentToken = await ApiFactory.LoginAsync(client, "student@appointmentsystem.com", "Student123!");
        ApiFactory.Authenticate(client, advisorToken);
        var advisorId = await ApiFactory.GetProfileIdAsync(client, "/api/Advisor/my-profile");
        await OpenAllWeekAsync(client, advisorToken);

        ApiFactory.Authenticate(client, studentToken);
        var created = await client.PostAsJsonAsync("/api/Appointment", new
        {
            advisorId,
            subject = "Next week",
            startTime = DateTime.Today.AddDays(7).AddHours(10),
            endTime = DateTime.Today.AddDays(7).AddHours(10).AddMinutes(30),
            appointmentType = "General"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var processed = await factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<IReminderService>()
            .SendDueRemindersAsync(DateTime.Now);

        Assert.Equal(0, processed);
        Assert.Equal(0, factory.Emails.Sent.Count(s => s.Kind == "reminder"));
    }
}

public class PasswordResetTests
{
    private static async Task<HttpResponseMessage> ForgotAsync(HttpClient client, string email) =>
        await client.PostAsJsonAsync("/api/Auth/forgot-password", new { email });

    private static async Task<HttpResponseMessage> ResetAsync(HttpClient client, string email, string token, string password) =>
        await client.PostAsJsonAsync("/api/Auth/reset-password", new
        {
            email,
            token,
            newPassword = password,
            confirmPassword = password
        });

    [Fact]
    public async Task ForgotPassword_SendsTokenEmail_AndReset_ChangesPassword()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();

        var register = await client.PostAsJsonAsync("/api/Auth/register", new
        {
            email = "reset.me@test.com",
            password = "OldPass1",
            confirmPassword = "OldPass1",
            firstName = "Reset",
            lastName = "Me",
            role = "Advisor"
        });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await ForgotAsync(client, "reset.me@test.com")).StatusCode);
        var token = factory.Emails.ResetTokens["reset.me@test.com"];

        var reset = await ResetAsync(client, "reset.me@test.com", token, "NewPass1");
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        var oldLogin = await client.PostAsJsonAsync("/api/Auth/login", new { email = "reset.me@test.com", password = "OldPass1" });
        var newLogin = await client.PostAsJsonAsync("/api/Auth/login", new { email = "reset.me@test.com", password = "NewPass1" });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_ReturnsSameAnswer_AndSendsNothing()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();

        var response = await ForgotAsync(client, "nobody@test.com");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, factory.Emails.Sent.Count(s => s.Kind == "password-reset"));
    }

    [Fact]
    public async Task ResetPassword_WithWrongToken_IsRejected()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();

        var response = await ResetAsync(client, "student@appointmentsystem.com", "not-a-real-token", "NewPass1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_TokenCannotBeReused()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();

        await ForgotAsync(client, "student@appointmentsystem.com");
        var token = factory.Emails.ResetTokens["student@appointmentsystem.com"];

        Assert.Equal(HttpStatusCode.OK, (await ResetAsync(client, "student@appointmentsystem.com", token, "NewPass1")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(client, "student@appointmentsystem.com", token, "Another1")).StatusCode);
    }

    [Fact]
    public async Task ResetPassword_WeakPassword_IsRejected()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();

        await ForgotAsync(client, "student@appointmentsystem.com");
        var token = factory.Emails.ResetTokens["student@appointmentsystem.com"];

        // Too short for the Identity password rules
        var response = await ResetAsync(client, "student@appointmentsystem.com", token, "abc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
