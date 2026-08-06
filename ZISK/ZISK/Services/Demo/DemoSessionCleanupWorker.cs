using Microsoft.EntityFrameworkCore;
using ZISK.Data;

namespace ZISK.Services.Demo;

/// <summary>
/// Deletes demo sessions that have gone quiet for more than 24h, independent of the visitor's
/// 30-day session cookie - the cookie only lets a returning visitor find their session *if it
/// still exists*; the session's actual data lifetime is much shorter, which is what keeps a
/// public demo's database from growing without bound.
/// </summary>
public class DemoSessionCleanupWorker : BackgroundService
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly StartupState _startupState;
    private readonly ILogger<DemoSessionCleanupWorker> _logger;

    public DemoSessionCleanupWorker(
        IServiceScopeFactory scopeFactory,
        StartupState startupState,
        ILogger<DemoSessionCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _startupState = startupState;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Reads DemoSessions on the first tick, before any Task.Delay, so it needs the schema.
        await _startupState.WaitForReadyAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

                if (SeedModeResolver.Resolve(configuration) == SeedMode.Demo)
                {
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var sessionService = scope.ServiceProvider.GetRequiredService<IDemoSessionService>();

                    var cutoff = DateTime.UtcNow - StaleAfter;
                    var staleIds = await context.DemoSessions
                        .Where(s => s.LastSeenAt < cutoff)
                        .Select(s => s.Id)
                        .ToListAsync(stoppingToken);

                    foreach (var id in staleIds)
                    {
                        await sessionService.DeleteSessionDataAsync(id, alsoDeleteSessionRow: true);
                        _logger.LogInformation("Cleaned up stale demo session {SessionId}", id);
                    }

                    if (staleIds.Count > 0)
                        _logger.LogInformation("Demo session cleanup removed {Count} stale session(s)", staleIds.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DemoSessionCleanupWorker pass failed");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
