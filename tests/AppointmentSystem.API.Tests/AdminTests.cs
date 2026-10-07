using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace AppointmentSystem.API.Tests;

public class AdminTests
{
    private static readonly DateTime Day = DateTime.Today.AddDays(7);

    private static async Task<HttpResponseMessage> RegisterStudentAsync(HttpClient client, string email, string number)
    {
        return await client.PostAsJsonAsync("/api/Auth/register", new
        {
            email,
            password = "Passw0rd",
            confirmPassword = "Passw0rd",
            firstName = "Test",
            lastName = "Student",
            role = "Student",
            studentNumber = number
        });
    }

    [Fact]
    public async Task Admin_CanConfirmAndCompleteAnyAppointment()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();

        var advisorToken = await ApiFactory.LoginAsync(client, "advisor@appointmentsystem.com", "Advisor123!");
        var studentToken = await ApiFactory.LoginAsync(client, "student@appointmentsystem.com", "Student123!");
        var adminToken = await ApiFactory.LoginAsync(client, "admin@appointmentsystem.com", "Admin123!");

        ApiFactory.Authenticate(client, advisorToken);
        var advisorId = await ApiFactory.GetProfileIdAsync(client, "/api/Advisor/my-profile");
        for (var day = 0; day < 7; day++)
        {
            (await client.PostAsJsonAsync("/api/Availability", new { dayOfWeek = day, startTime = "00:00:00", endTime = "23:59:00", isRecurring = true })).EnsureSuccessStatusCode();
        }

        ApiFactory.Authenticate(client, studentToken);
        var created = await client.PostAsJsonAsync("/api/Appointment", new
        {
            advisorId,
            subject = "Admin test",
            startTime = Day.AddHours(10),
            endTime = Day.AddHours(10).AddMinutes(30),
            appointmentType = "General"
        });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        ApiFactory.Authenticate(client, adminToken);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/Appointment/{id}/confirm", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/Appointment/{id}/complete", null)).StatusCode);
    }

    [Fact]
    public async Task Admin_CanListAllUsers_NonAdminCannot()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();

        var adminToken = await ApiFactory.LoginAsync(client, "admin@appointmentsystem.com", "Admin123!");
        var studentToken = await ApiFactory.LoginAsync(client, "student@appointmentsystem.com", "Student123!");

        ApiFactory.Authenticate(client, adminToken);
        var users = await (await client.GetAsync("/api/Admin/users")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, users.GetArrayLength());

        ApiFactory.Authenticate(client, studentToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/Admin/users")).StatusCode);
    }

    [Fact]
    public async Task Admin_DeletingStudent_RemovesAccountAndAppointments()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();

        var advisorToken = await ApiFactory.LoginAsync(client, "advisor@appointmentsystem.com", "Advisor123!");
        var adminToken = await ApiFactory.LoginAsync(client, "admin@appointmentsystem.com", "Admin123!");

        ApiFactory.Authenticate(client, advisorToken);
        var advisorId = await ApiFactory.GetProfileIdAsync(client, "/api/Advisor/my-profile");
        for (var day = 0; day < 7; day++)
        {
            (await client.PostAsJsonAsync("/api/Availability", new { dayOfWeek = day, startTime = "00:00:00", endTime = "23:59:00", isRecurring = true })).EnsureSuccessStatusCode();
        }

        var register = await RegisterStudentAsync(client, "to.delete@test.com", "5555");
        var studentToken = (await register.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        ApiFactory.Authenticate(client, studentToken);
        var booked = await client.PostAsJsonAsync("/api/Appointment", new
        {
            advisorId,
            subject = "Will be deleted",
            startTime = Day.AddHours(11),
            endTime = Day.AddHours(11).AddMinutes(30),
            appointmentType = "General"
        });
        Assert.Equal(HttpStatusCode.Created, booked.StatusCode);

        ApiFactory.Authenticate(client, adminToken);
        var users = await (await client.GetAsync("/api/Admin/users")).Content.ReadFromJsonAsync<JsonElement>();
        var userId = users.EnumerateArray().First(u => u.GetProperty("email").GetString() == "to.delete@test.com").GetProperty("id").GetString()!;

        var delete = await client.DeleteAsync($"/api/Admin/users/{userId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var login = await client.PostAsJsonAsync("/api/Auth/login", new { email = "to.delete@test.com", password = "Passw0rd" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);

        ApiFactory.Authenticate(client, advisorToken);
        var advisorList = await (await client.GetAsync("/api/Appointment?pageSize=100")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.DoesNotContain(advisorList.EnumerateArray(), a => a.GetProperty("subject").GetString() == "Will be deleted");
    }

    [Fact]
    public async Task Admin_CannotDeleteOwnAccount()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();

        var adminToken = await ApiFactory.LoginAsync(client, "admin@appointmentsystem.com", "Admin123!");
        ApiFactory.Authenticate(client, adminToken);
        var users = await (await client.GetAsync("/api/Admin/users")).Content.ReadFromJsonAsync<JsonElement>();
        var adminId = users.EnumerateArray().First(u => u.GetProperty("role").GetString() == "Admin").GetProperty("id").GetString()!;

        var response = await client.DeleteAsync($"/api/Admin/users/{adminId}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanDeleteAndUpdateAnyAvailability()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();

        var advisorToken = await ApiFactory.LoginAsync(client, "advisor@appointmentsystem.com", "Advisor123!");
        var adminToken = await ApiFactory.LoginAsync(client, "admin@appointmentsystem.com", "Admin123!");

        ApiFactory.Authenticate(client, advisorToken);
        var advisorId = await ApiFactory.GetProfileIdAsync(client, "/api/Advisor/my-profile");
        var created = await client.PostAsJsonAsync("/api/Availability", new { dayOfWeek = 2, startTime = "09:00:00", endTime = "10:00:00", isRecurring = true });
        var availabilityId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        ApiFactory.Authenticate(client, adminToken);
        var list = await (await client.GetAsync($"/api/Availability/advisor/{advisorId}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(list.EnumerateArray());

        var update = await client.PutAsJsonAsync($"/api/Availability/{availabilityId}", new { endTime = "11:00:00" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var delete = await client.DeleteAsync($"/api/Availability/{availabilityId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task Admin_CanCreateAvailabilityForAdvisor()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();

        var advisorToken = await ApiFactory.LoginAsync(client, "advisor@appointmentsystem.com", "Advisor123!");
        var adminToken = await ApiFactory.LoginAsync(client, "admin@appointmentsystem.com", "Admin123!");

        ApiFactory.Authenticate(client, advisorToken);
        var advisorId = await ApiFactory.GetProfileIdAsync(client, "/api/Advisor/my-profile");

        ApiFactory.Authenticate(client, adminToken);
        var created = await client.PostAsJsonAsync("/api/Availability", new
        {
            advisorId,
            dayOfWeek = (int)DayOfWeek.Friday,
            startTime = "09:00:00",
            endTime = "12:00:00",
            isRecurring = true
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var list = await (await client.GetAsync($"/api/Availability/advisor/{advisorId}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(list.EnumerateArray());
    }

    [Fact]
    public async Task Admin_CreatingAvailability_WithoutAdvisorId_IsRejected()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();

        var adminToken = await ApiFactory.LoginAsync(client, "admin@appointmentsystem.com", "Admin123!");
        ApiFactory.Authenticate(client, adminToken);

        var response = await client.PostAsJsonAsync("/api/Availability", new
        {
            dayOfWeek = (int)DayOfWeek.Friday,
            startTime = "09:00:00",
            endTime = "12:00:00",
            isRecurring = true
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
