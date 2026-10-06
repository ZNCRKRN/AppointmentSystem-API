using AppointmentSystem.API.Data;
using AppointmentSystem.API.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net.Http.Json;
using System.Text.Json;

namespace AppointmentSystem.API.Tests;

/// <summary>
/// Hosts the real API pipeline (auth, controllers, services) against an isolated SQLite in-memory database.
/// SQLite (unlike EF's InMemory provider) enforces unique indexes, so constraint behaviour matches SQL Server.
/// Emails are replaced with a no-op sender so tests never talk to an SMTP server.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    // The connection must stay open for the lifetime of the in-memory database
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public ApiFactory()
    {
        _connection.Open();

        // Program.cs does not create the schema, so create it before the host seeds data
        var options = new DbContextOptionsBuilder<AppointmentDbContext>().UseSqlite(_connection).Options;
        using var context = new AppointmentDbContext(options);
        context.Database.EnsureCreated();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // Real secrets live in user-secrets, which the test host does not load; use a fixed test key
        builder.UseSetting("Jwt:SecretKey", "test-only-secret-key-that-is-at-least-32-chars-long");

        builder.ConfigureServices(services =>
        {
            // Remove the SQL Server provider registrations so only SQLite is configured
            var dbDescriptors = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppointmentDbContext>)
                         || d.ServiceType == typeof(DbContextOptions)
                         || d.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration"))
                .ToList();
            foreach (var descriptor in dbDescriptors)
                services.Remove(descriptor);

            services.AddDbContext<AppointmentDbContext>(options => options.UseSqlite(_connection));

            services.RemoveAll<IEmailService>();
            services.AddSingleton<RecordingEmailService>();
            services.AddScoped<IEmailService>(sp => sp.GetRequiredService<RecordingEmailService>());
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }

    public HttpClient CreateApiClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    public static async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/Auth/login", new { email, password });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }

    public static void Authenticate(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    public static async Task<int> GetProfileIdAsync(HttpClient client, string profilePath)
    {
        var response = await client.GetAsync(profilePath);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetInt32();
    }

    /// <summary>Gets the test email service registered by the factory (singleton, so tests can inspect it).</summary>
    public RecordingEmailService Emails => Services.GetRequiredService<RecordingEmailService>();
}

/// <summary>Sends nothing; records what would have been sent so tests can assert on it.</summary>
public sealed class RecordingEmailService : IEmailService
{
    public List<(string Kind, string Recipient)> Sent { get; } = new();
    public Dictionary<string, string> ResetTokens { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Task SendAppointmentRequestedAsync(AppointmentSystem.API.DTOs.AppointmentResponse appointment, string recipientEmail, string recipientName, bool isAdvisorRecipient)
    {
        Sent.Add(("requested", recipientEmail));
        return Task.CompletedTask;
    }

    public Task SendAppointmentConfirmationAsync(AppointmentSystem.API.DTOs.AppointmentResponse appointment, string recipientEmail, string recipientName)
    {
        Sent.Add(("confirmed", recipientEmail));
        return Task.CompletedTask;
    }

    public Task SendAppointmentReminderAsync(AppointmentSystem.API.DTOs.AppointmentResponse appointment, string recipientEmail, string recipientName)
    {
        Sent.Add(("reminder", recipientEmail));
        return Task.CompletedTask;
    }

    public Task SendAppointmentCancellationAsync(AppointmentSystem.API.DTOs.AppointmentResponse appointment, string recipientEmail, string recipientName)
    {
        Sent.Add(("cancelled", recipientEmail));
        return Task.CompletedTask;
    }

    public Task SendWelcomeEmailAsync(string email, string name, string role)
    {
        Sent.Add(("welcome", email));
        return Task.CompletedTask;
    }

    public Task SendPasswordResetEmailAsync(string email, string resetToken)
    {
        Sent.Add(("password-reset", email));
        ResetTokens[email] = resetToken;
        return Task.CompletedTask;
    }
}
