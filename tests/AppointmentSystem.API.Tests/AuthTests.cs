using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace AppointmentSystem.API.Tests;

public class AuthTests
{
    [Fact]
    public async Task Login_WithSeededStudent_ReturnsTokenAndSingleRole()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();

        var response = await client.PostAsJsonAsync("/api/Auth/login",
            new { email = "student@appointmentsystem.com", password = "Student123!" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(body.GetProperty("token").GetString()));
        // The frontend relies on a single "role" string on the user object
        Assert.Equal("Student", body.GetProperty("user").GetProperty("role").GetString());
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();

        var response = await client.PostAsJsonAsync("/api/Auth/login",
            new { email = "student@appointmentsystem.com", password = "WrongPass1" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_AsAdmin_IsRejected()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();

        var response = await client.PostAsJsonAsync("/api/Auth/register", new
        {
            email = "sneaky@test.com",
            password = "Passw0rd",
            confirmPassword = "Passw0rd",
            firstName = "Sneaky",
            lastName = "Admin",
            role = "Admin"
        });

        // Admin accounts must never be creatable through public registration
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_StudentWithoutStudentNumber_IsRejected()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();

        var response = await client.PostAsJsonAsync("/api/Auth/register", new
        {
            email = "nonum@test.com",
            password = "Passw0rd",
            confirmPassword = "Passw0rd",
            firstName = "No",
            lastName = "Number",
            role = "Student"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_Advisor_CreatesAccountThatCanLogin()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();

        var register = await client.PostAsJsonAsync("/api/Auth/register", new
        {
            email = "newadvisor@test.com",
            password = "Passw0rd",
            confirmPassword = "Passw0rd",
            firstName = "New",
            lastName = "Advisor",
            role = "Advisor",
            department = "Mathematics"
        });

        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var token = await ApiFactory.LoginAsync(client, "newadvisor@test.com", "Passw0rd");
        Assert.False(string.IsNullOrEmpty(token));
    }

    [Fact]
    public async Task Register_DuplicateStudentNumber_FailsAndLeavesNoOrphanAccount()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();

        var first = await client.PostAsJsonAsync("/api/Auth/register", new
        {
            email = "first@test.com",
            password = "Passw0rd",
            confirmPassword = "Passw0rd",
            firstName = "First",
            lastName = "Student",
            role = "Student",
            studentNumber = "9999"
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Same student number violates the unique index on Students.StudentNumber
        var second = await client.PostAsJsonAsync("/api/Auth/register", new
        {
            email = "second@test.com",
            password = "Passw0rd",
            confirmPassword = "Passw0rd",
            firstName = "Second",
            lastName = "Student",
            role = "Student",
            studentNumber = "9999"
        });
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);

        // The identity user created before the failure must have been rolled back
        var login = await client.PostAsJsonAsync("/api/Auth/login",
            new { email = "second@test.com", password = "Passw0rd" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }
}
