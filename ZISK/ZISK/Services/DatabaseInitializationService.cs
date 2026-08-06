namespace ZISK.Services;

/// <summary>
/// Runs the one-time database migration and seed off the startup path.
///
/// This used to sit inline in Program.cs, between the endpoint mapping and <c>app.Run()</c>. That
/// ordering meant Kestrel did not bind a port until migrations and seeding had finished, and on the
/// demo deployment that is the dominant cause of a blank first load: App Service starts the
/// container, Azure SQL serverless is auto-paused, and the retry loop below can spend the better
/// part of a minute waiting for the resume - all of it with no listener, so the browser sits on a
/// request that has not returned a single header and there is nothing to render. Running the same
/// loop here instead lets the server answer immediately, and StartupGateMiddleware serves a real
/// "waking up" page for that window.
///
/// The safety property the old placement provided is preserved, just enforced differently: the app
/// still never serves a request against a schema-less database, because StartupGateMiddleware holds
/// every request until <see cref="StartupState.IsReady"/>.
/// </summary>
public sealed class DatabaseInitializationService : BackgroundService
{
    /// <summary>Attempt at which a transient failure stops being routine and gets logged as critical.</summary>
    private const int LoudAfterAttempts = 10;

    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly StartupState _state;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<DatabaseInitializationService> _logger;

    public DatabaseInitializationService(
        IServiceScopeFactory scopeFactory,
        StartupState state,
        IHostApplicationLifetime lifetime,
        ILogger<DatabaseInitializationService> logger)
    {
        _scopeFactory = scopeFactory;
        _state = state;
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = InitialDelay;

        for (var attempt = 1; !stoppingToken.IsCancellationRequested; attempt++)
        {
            _state.RecordAttempt(attempt);

            // A fresh scope per attempt: a DbContext that failed mid-migration must not be reused.
            using var scope = _scopeFactory.CreateScope();
            var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();

            try
            {
                await initializer.InitializeAsync(_state.EnterPhase);
                _state.MarkReady();

                if (attempt > 1)
                    _logger.LogInformation("Database initialization succeeded on attempt {Attempt}.", attempt);

                return;
            }
            catch (SeedConfigurationException ex)
            {
                // A demo/production deploy with missing Seed:* configuration must fail loudly at
                // startup, not silently fall back to hardcoded local passwords or run with no admin.
                // Retrying cannot help - the configuration is wrong, not the database - so bring the
                // host down, which is the same outcome as this exception escaping Program.cs before.
                _logger.LogCritical(ex, "Seed configuration is invalid; shutting down.");
                _state.MarkFailed(ex.Message);
                _lifetime.StopApplication();
                return;
            }
            catch (Exception ex)
            {
                if (attempt < LoudAfterAttempts)
                {
                    _state.EnterPhase(StartupPhase.WakingDatabase);
                    _logger.LogWarning(ex,
                        "Database initialization failed (attempt {Attempt}/{LoudAfter}); retrying in {Delay}s.",
                        attempt, LoudAfterAttempts, delay.TotalSeconds);
                }
                else if (attempt == LoudAfterAttempts)
                {
                    // Deliberately louder than the retries above, and the point at which visitors
                    // start seeing an error instead of a "waking up" page. Retrying continues anyway:
                    // on a free-tier demo the usual cause is a database that is slow rather than
                    // broken, and an app that heals itself once it comes back beats one that has to
                    // be redeployed by hand.
                    _state.MarkFailed(ex.Message);
                    _logger.LogCritical(ex,
                        "Database initialization has failed {Attempt} times. Serving an error page and continuing to retry.",
                        attempt);
                }
                else
                {
                    _logger.LogWarning(ex, "Database initialization still failing (attempt {Attempt}).", attempt);
                }

                await Task.Delay(delay, stoppingToken);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 1.5, MaxDelay.TotalSeconds));
            }
        }
    }
}
