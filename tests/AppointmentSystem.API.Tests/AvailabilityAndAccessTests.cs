using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace AppointmentSystem.API.Tests;

public class AvailabilityAndAccessTests
{
    private static async Task<(HttpClient client, ApiFactory factory, int advisorId, string studentToken, string advisorToken, string adminToken)> SetupAsync()
    {
        var factory = new ApiFactory();
        var client = factory.CreateApiClient();

        var advisorToken = await ApiFactory.LoginAsync(client, "advisor@appointmentsystem.com", "Advisor123!");
        var studentToken = await ApiFactory.LoginAsync(client, "student@appointmentsystem.com", "Student123!");
        var adminToken = await ApiFactory.LoginAsync(client, "admin@appointmentsystem.com", "Admin123!");

        ApiFactory.Authenticate(client, advisorToken);
        var advisorId = await ApiFactory.GetProfileIdAsync(client, "/api/Advisor/my-profile");

        return (client, factory, advisorId, studentToken, advisorToken, adminToken);
    }

    // Next occurrence of the given weekday, at least 7 days out so it is always in the future
    private static DateTime NextDayOfWeek(DayOfWeek dayOfWeek)
    {
        var date = DateTime.Today.AddDays(7);
        while (date.DayOfWeek != dayOfWeek)
            date = date.AddDays(1);
        return date;
    }

    [Fact]
    public async Task Availability_EndBeforeStart_IsRejected()
    {
        var (client, factory, _, _, _, _) = await SetupAsync();
        using var _f = factory;
        using var _c = client;

        var response = await client.PostAsJsonAsync("/api/Availability", new
        {
            dayOfWeek = (int)DayOfWeek.Monday,
            startTime = "17:00:00",
            endTime = "09:00:00",
            isRecurring = true
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Booking_OutsideAdvisorAvailability_IsRejected_InsideIsAccepted()
    {
        var (client, factory, advisorId, studentToken, advisorToken, _) = await SetupAsync();
        using var _f = factory;
        using var _c = client;

        var date = NextDayOfWeek(DayOfWeek.Thursday);

        // Advisor is available Thursdays 09:00-11:00 only
        ApiFactory.Authenticate(client, advisorToken);
        await client.PostAsJsonAsync("/api/Availability", new
        {
            dayOfWeek = (int)DayOfWeek.Thursday,
            startTime = "09:00:00",
            endTime = "11:00:00",
            isRecurring = true
        });

        ApiFactory.Authenticate(client, studentToken);
        object Booking(DateTime start) => new
        {
            advisorId,
            subject = "Advice",
            startTime = start,
            endTime = start.AddMinutes(30),
            appointmentType = "General"
        };

        var outside = await client.PostAsJsonAsync("/api/Appointment", Booking(date.AddHours(13)));
        Assert.Equal(HttpStatusCode.BadRequest, outside.StatusCode);

        // A slot that starts inside the window but runs past its end is also outside
        var overrun = await client.PostAsJsonAsync("/api/Appointment", new
        {
            advisorId,
            subject = "Advice",
            startTime = date.AddHours(10).AddMinutes(45),
            endTime = date.AddHours(11).AddMinutes(15),
            appointmentType = "General"
        });
        Assert.Equal(HttpStatusCode.BadRequest, overrun.StatusCode);

        var inside = await client.PostAsJsonAsync("/api/Appointment", Booking(date.AddHours(9)));
        Assert.Equal(HttpStatusCode.Created, inside.StatusCode);
    }

    [Fact]
    public async Task Student_CannotListAvailabilities_AndGetsForbiddenNotServerError()
    {
        var (client, factory, _, studentToken, _, _) = await SetupAsync();
        using var _f = factory;
        using var _c = client;

        ApiFactory.Authenticate(client, studentToken);
        var response = await client.GetAsync("/api/Availability");

        // Previously Forbid("message") was interpreted as an auth scheme name and crashed with a 500
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AvailableSlots_IgnoreSoftDeletedAppointments()
    {
        var (client, factory, advisorId, studentToken, advisorToken, _) = await SetupAsync();
        using var _f = factory;
        using var _c = client;

        var date = NextDayOfWeek(DayOfWeek.Wednesday);

        // Advisor works 09:00-11:00 every Wednesday
        ApiFactory.Authenticate(client, advisorToken);
        var availability = await client.PostAsJsonAsync("/api/Availability", new
        {
            dayOfWeek = (int)DayOfWeek.Wednesday,
            startTime = "09:00:00",
            endTime = "11:00:00",
            isRecurring = true
        });
        Assert.Equal(HttpStatusCode.Created, availability.StatusCode);

        // Student books the 09:00 slot, then cancels it via soft delete
        ApiFactory.Authenticate(client, studentToken);
        var booked = await client.PostAsJsonAsync("/api/Appointment", new
        {
            advisorId,
            subject = "Quick question",
            startTime = date.AddHours(9),
            endTime = date.AddHours(9).AddMinutes(30),
            appointmentType = "General"
        });
        var appointmentId = (await booked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.Created, booked.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/Appointment/{appointmentId}")).StatusCode);

        // The 09:00 slot must be free again; before the fix the soft-deleted row kept it blocked
        var slots = await client.PostAsJsonAsync("/api/Availability/available-slots", new
        {
            advisorId,
            date,
            durationMinutes = 30
        });
        Assert.Equal(HttpStatusCode.OK, slots.StatusCode);

        var nine = (await slots.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()
            .First(s => s.GetProperty("startTime").GetDateTime() == date.AddHours(9));
        Assert.True(nine.GetProperty("isAvailable").GetBoolean());
    }

    [Fact]
    public async Task AvailableSlots_DurationZero_IsRejectedInsteadOfLooping()
    {
        var (client, factory, advisorId, _, _, _) = await SetupAsync();
        using var _f = factory;
        using var _c = client;

        var response = await client.PostAsJsonAsync("/api/Availability/available-slots", new
        {
            advisorId,
            date = NextDayOfWeek(DayOfWeek.Monday),
            durationMinutes = 0
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AvailableSlots_NeverOfferPastTimes()
    {
        var (client, factory, advisorId, _, advisorToken, _) = await SetupAsync();
        using var _f = factory;
        using var _c = client;

        var today = DateTime.Today;
        ApiFactory.Authenticate(client, advisorToken);
        await client.PostAsJsonAsync("/api/Availability", new
        {
            dayOfWeek = (int)today.DayOfWeek,
            startTime = "00:00:00",
            endTime = "23:45:00",
            isRecurring = true
        });

        var before = DateTime.Now;
        var response = await client.PostAsJsonAsync("/api/Availability/available-slots", new
        {
            advisorId,
            date = today,
            durationMinutes = 15
        });

        var slots = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        Assert.All(slots, s => Assert.True(s.GetProperty("startTime").GetDateTime() >= before.AddMinutes(-15)));
    }

    [Fact]
    public async Task Admin_CanListStudents_StudentCannot()
    {
        var (client, factory, _, studentToken, _, adminToken) = await SetupAsync();
        using var _f = factory;
        using var _c = client;

        ApiFactory.Authenticate(client, adminToken);
        var adminView = await client.GetAsync("/api/Student");
        Assert.Equal(HttpStatusCode.OK, adminView.StatusCode);
        Assert.Single((await adminView.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());

        ApiFactory.Authenticate(client, studentToken);
        var studentView = await client.GetAsync("/api/Student");
        Assert.Equal(HttpStatusCode.Forbidden, studentView.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_RequestsAreRejected()
    {
        var (client, factory, _, _, _, _) = await SetupAsync();
        using var _f = factory;
        using var _c = client;

        client.DefaultRequestHeaders.Authorization = null;
        var response = await client.GetAsync("/api/Appointment");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
