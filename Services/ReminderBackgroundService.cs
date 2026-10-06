namespace AppointmentSystem.API.Services
{
    // Runs the reminder check on a fixed interval; each pass gets its own DI scope so the DbContext is fresh
    public class ReminderBackgroundService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ReminderBackgroundService> _logger;

        public ReminderBackgroundService(IServiceScopeFactory scopeFactory, ILogger<ReminderBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Interval);

            do
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var reminders = scope.ServiceProvider.GetRequiredService<IReminderService>();
                    var processed = await reminders.SendDueRemindersAsync(DateTime.Now);

                    if (processed > 0)
                        _logger.LogInformation("Sent reminders for {Count} appointment(s)", processed);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Reminder pass failed");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
