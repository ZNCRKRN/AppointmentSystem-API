using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace AppointmentSystem.API.Tests;

public class AppointmentTests
{
    // Dates are a week ahead so they are always in the future and avoid "today" edge cases
    private static readonly DateTime Day = DateTime.Today.AddDays(7);

    private static async Task<(HttpClient client, ApiFactory factory, int advisorId, string studentToken, string advisorToken)> SetupAsync()
    {
        var factory = new ApiFactory();
        var client = factory.CreateApiClient();

        var advisorToken = await ApiFactory.LoginAsync(client, "advisor@appointmentsystem.com", "Advisor123!");
        var studentToken = await ApiFactory.LoginAsync(client, "student@appointmentsystem.com", "Student123!");

        ApiFactory.Authenticate(client, advisorToken);
        var advisorId = await ApiFactory.GetProfileIdAsync(client, "/api/Advisor/my-profile");

        // Bookings must fall inside availability, so open the whole day on every weekday
        for (var day = 0; day < 7; day++)
        {
            var response = await client.PostAsJsonAsync("/api/Availability", new
            {
                dayOfWeek = day,
                startTime = "00:00:00",
                endTime = "23:59:00",
                isRecurring = true
            });
            response.EnsureSuccessStatusCode();
        }

        return (client, factory, advisorId, studentToken, advisorToken);
    }

    private static object NewAppointment(int advisorId, DateTime start, DateTime end) => new
    {
        advisorId,
        subject = "Thesis review",
        description = "Chapter 3",
        startTime = start,
        endTime = end,
        appointmentType = "Academic"
    };

    private static async Task<HttpResponseMessage> CreateAsAsync(HttpClient client, string token, object body)
    {
        ApiFactory.Authenticate(client, token);
        return await client.PostAsJsonAsync("/api/Appointment", body);
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Student_CanBookFreeSlot()
    {
        var (client, factory, advisorId, studentToken, _) = await SetupAsync();
        using var _f = factory;
        using var _c = client;

        var response = await CreateAsAsync(client, studentToken,
            NewAppointment(advisorId, Day.AddHours(10), Day.AddHours(10).AddMinutes(30)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("Scheduled", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Booking_OverlappingExistingAppointment_IsRejected()
    {
        var (client, factory, advisorId, studentToken, _) = await SetupAsync();
        using var _f = factory;
        using var _c = client;

        var first = await CreateAsAsync(client, studentToken,
            NewAppointment(advisorId, Day.AddHours(10), Day.AddHours(11)));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var overlapping = await CreateAsAsync(client, studentToken,
            NewAppointment(advisorId, Day.AddHours(10).AddMinutes(30), Day.AddHours(11).AddMinutes(30)));

        Assert.Equal(HttpStatusCode.BadRequest, overlapping.StatusCode);
    }

    [Fact]
    public async Task Booking_BackToBack_IsAllowed()
    {
        var (client, factory, advisorId, studentToken, _) = await SetupAsync();
        using var _f = factory;
        using var _c = client;

        var first = await CreateAsAsync(client, studentToken,
            NewAppointment(advisorId, Day.AddHours(10), Day.AddHours(11)));
        var next = await CreateAsAsync(client, studentToken,
            NewAppointment(advisorId, Day.AddHours(11), Day.AddHours(12)));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, next.StatusCode);
    }

    [Fact]
    public async Task Booking_EndBeforeStart_IsRejected()
    {
        var (client, factory, advisorId, studentToken, _) = await SetupAsync();
        using var _f = factory;
        using var _c = client;

        var response = await CreateAsAsync(client, studentToken,
            NewAppointment(advisorId, Day.AddHours(11), Day.AddHours(10)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Booking_InThePast_IsRejected()
    {
        var (client, factory, advisorId, studentToken, _) = await SetupAsync();
        using var _f = factory;
        using var _c = client;

        var past = DateTime.Now.AddDays(-1);
        var response = await CreateAsAsync(client, studentToken,
            NewAppointment(advisorId, past, past.AddHours(1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Student_CannotReadAnotherStudentsAppointment()
    {
        var (client, factory, advisorId, studentToken, _) = await SetupAsync();
        using var _f = factory;
        using var _c = client;

        var created = await CreateAsAsync(client, studentToken,
            NewAppointment(advisorId, Day.AddHours(9), Day.AddHours(9).AddMinutes(30)));
        var id = (await ReadAsync(created)).GetProperty("id").GetInt32();

        var other = await client.PostAsJsonAsync("/api/Auth/register", new
        {
            email = "other.student@test.com",
            password = "Passw0rd",
            confirmPassword = "Passw0rd",
            firstName = "Other",
            lastName = "Student",
            role = "Student",
            studentNumber = "7777"
        });
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
        var otherToken = (await ReadAsync(other)).GetProperty("token").GetString()!;

        ApiFactory.Authenticate(client, otherToken);
        var response = await client.GetAsync($"/api/Appointment/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Advisor_CanConfirm_ThenComplete()
    {
        var (client, factory, advisorId, studentToken, advisorToken) = await SetupAsync();
        using var _f = factory;
        using var _c = client;

        var created = await CreateAsAsync(client, studentToken,
            NewAppointment(advisorId, Day.AddHours(13), Day.AddHours(13).AddMinutes(30)));
        var id = (await ReadAsync(created)).GetProperty("id").GetInt32();

        ApiFactory.Authenticate(client, advisorToken);
        var confirm = await client.PostAsync($"/api/Appointment/{id}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        var complete = await client.PostAsync($"/api/Appointment/{id}/complete", null);
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);

        var final = await client.GetAsync($"/api/Appointment/{id}");
        Assert.Equal("Completed", (await ReadAsync(final)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Complete_BeforeConfirm_IsRejected()
    {
        var (client, factory, advisorId, studentToken, advisorToken) = await SetupAsync();
        using var _f = factory;
        using var _c = client;

        var created = await CreateAsAsync(client, studentToken,
            NewAppointment(advisorId, Day.AddHours(14), Day.AddHours(14).AddMinutes(30)));
        var id = (await ReadAsync(created)).GetProperty("id").GetInt32();

        ApiFactory.Authenticate(client, advisorToken);
        var response = await client.PostAsync($"/api/Appointment/{id}/complete", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cancelled_AppointmentCannotBeConfirmed()
    {
        var (client, factory, advisorId, studentToken, advisorToken) = await SetupAsync();
        using var _f = factory;
        using var _c = client;

        var created = await CreateAsAsync(client, studentToken,
            NewAppointment(advisorId, Day.AddHours(15), Day.AddHours(15).AddMinutes(30)));
        var id = (await ReadAsync(created)).GetProperty("id").GetInt32();

        ApiFactory.Authenticate(client, studentToken);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/Appointment/{id}/cancel", null)).StatusCode);

        ApiFactory.Authenticate(client, advisorToken);
        var confirm = await client.PostAsync($"/api/Appointment/{id}/confirm", null);

        // Previously a cancelled appointment could be silently re-confirmed
        Assert.Equal(HttpStatusCode.NotFound, confirm.StatusCode);
    }

    [Fact]
    public async Task Update_CannotChangeStatusDirectly()
    {
        var (client, factory, advisorId, studentToken, _) = await SetupAsync();
        using var _f = factory;
        using var _c = client;

        var created = await CreateAsAsync(client, studentToken,
            NewAppointment(advisorId, Day.AddHours(16), Day.AddHours(16).AddMinutes(30)));
        var id = (await ReadAsync(created)).GetProperty("id").GetInt32();

        ApiFactory.Authenticate(client, studentToken);
        // "status" is not part of the update contract, so it must be ignored
        await client.PutAsJsonAsync($"/api/Appointment/{id}",
            new { status = "Confirmed", subject = "Renamed" });

        var final = await ReadAsync(await client.GetAsync($"/api/Appointment/{id}"));
        Assert.Equal("Scheduled", final.GetProperty("status").GetString());
        Assert.Equal("Renamed", final.GetProperty("subject").GetString());
    }

    [Fact]
    public async Task Reschedule_IntoAnotherAppointment_IsRejected()
    {
        var (client, factory, advisorId, studentToken, _) = await SetupAsync();
        using var _f = factory;
        using var _c = client;

        await CreateAsAsync(client, studentToken, NewAppointment(advisorId, Day.AddHours(17), Day.AddHours(18)));
        var movable = await CreateAsAsync(client, studentToken,
            NewAppointment(advisorId, Day.AddHours(19), Day.AddHours(20)));
        var id = (await ReadAsync(movable)).GetProperty("id").GetInt32();

        ApiFactory.Authenticate(client, studentToken);
        var response = await client.PutAsJsonAsync($"/api/Appointment/{id}",
            new { startTime = Day.AddHours(17).AddMinutes(30), endTime = Day.AddHours(18).AddMinutes(30) });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SoftDeletedAppointment_DisappearsFromLists()
    {
        var (client, factory, advisorId, studentToken, _) = await SetupAsync();
        using var _f = factory;
        using var _c = client;

        var created = await CreateAsAsync(client, studentToken,
            NewAppointment(advisorId, Day.AddHours(8), Day.AddHours(8).AddMinutes(30)));
        var id = (await ReadAsync(created)).GetProperty("id").GetInt32();

        ApiFactory.Authenticate(client, studentToken);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/Appointment/{id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/Appointment/{id}")).StatusCode);

        var list = await ReadAsync(await client.GetAsync("/api/Appointment?pageSize=100"));
        Assert.DoesNotContain(list.EnumerateArray(), a => a.GetProperty("id").GetInt32() == id);
    }

    [Fact]
    public async Task List_PageSizeIsClamped_ToAtLeastOne()
    {
        var (client, factory, advisorId, studentToken, _) = await SetupAsync();
        using var _f = factory;
        using var _c = client;

        await CreateAsAsync(client, studentToken, NewAppointment(advisorId, Day.AddHours(7), Day.AddHours(7).AddMinutes(30)));
        await CreateAsAsync(client, studentToken, NewAppointment(advisorId, Day.AddHours(6), Day.AddHours(6).AddMinutes(30)));

        ApiFactory.Authenticate(client, studentToken);
        // pageSize=0 previously produced an empty Take(0) page; it is now clamped to 1
        var response = await client.GetAsync("/api/Appointment?pageSize=0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single((await ReadAsync(response)).EnumerateArray());
    }
}
