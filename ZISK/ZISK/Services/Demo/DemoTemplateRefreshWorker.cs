namespace ZISK.Services.Demo;

/// <summary>
/// Hourly tick that keeps the shared demo template data from going stale. The actual
/// 24h-freshness check and regeneration logic lives in
/// <see cref="DatabaseInitializer.RefreshDemoTemplateAsync"/> (kept there, not here, so it can
/// be exercised directly against EF Core InMemory in tests without a background service in the
/// loop) - this class only provides the periodic trigger, the same shape as
/// AttendanceAutoCloseService. Registered unconditionally like the other hosted workers;
/// RefreshDemoTemplateAsync is a no-op outside ZISK_SEED_MODE=demo, so the idle check elsewhere
/// costs nothing.
/// </summary>
public class DemoTemplateRefreshWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly StartupState _startupState;
    private readonly ILogger<DemoTemplateRefreshWorker> _logger;

    public DemoTemplateRefreshWorker(
        IServiceScopeFactory scopeFactory,
        StartupState startupState,
        ILogger<DemoTemplateRefreshWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _startupState = startupState;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Refreshing the demo template writes to the database on the first tick, so it has to wait
        // for the seed it would otherwise be racing.
        await _startupState.WaitForReadyAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
                await initializer.RefreshDemoTemplateAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DemoTemplateRefreshWorker pass failed");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
