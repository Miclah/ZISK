using Microsoft.EntityFrameworkCore;
using ZISK.Data;
using ZISK.Data.Entities;

namespace ZISK.Services;

public class TrainingSeriesGeneratorWorker
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TrainingSeriesGeneratorWorker> _logger;

    public TrainingSeriesGeneratorWorker(ApplicationDbContext context, ILogger<TrainingSeriesGeneratorWorker> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task GenerateForSeriesAsync(Guid seriesId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var series = await _context.TrainingSeries
            .Include(ts => ts.Season)
            .FirstOrDefaultAsync(ts => ts.Id == seriesId, ct);

        if (series == null)
            return;

        var existingDates = await _context.TrainingEvents
            .Where(te => te.SeriesId == seriesId)
            .Select(te => DateOnly.FromDateTime(te.StartTime))
            .ToListAsync(ct);

        var existingSet = new HashSet<DateOnly>(existingDates);

        var instances = TrainingSeriesInstanceGenerator.BuildMissingInstances(series, from, to, existingSet);

        if (instances.Count > 0)
        {
            _context.TrainingEvents.AddRange(instances);
            await _context.SaveChangesAsync(ct);
        }

        _logger.LogInformation("Generated {Count} training instances for series {SeriesId}", instances.Count, seriesId);
    }

    public async Task RunPassAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizon = today.AddDays(14); // generate training instances up to 14 days in advance so coaches always see the next two weeks

        var activeSeries = await _context.TrainingSeries
            .Include(ts => ts.Season)
            .Where(ts => ts.IsActive && ts.Season.StartDate <= horizon && ts.Season.EndDate >= today)
            .ToListAsync(ct);

        foreach (var series in activeSeries)
        {
            try
            {
                await GenerateForSeriesAsync(series.Id, today, horizon, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate instances for series {SeriesId}", series.Id);
            }
        }
    }
}

public class TrainingSeriesGeneratorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly StartupState _startupState;
    private readonly ILogger<TrainingSeriesGeneratorService> _logger;

    public TrainingSeriesGeneratorService(
        IServiceScopeFactory scopeFactory,
        StartupState startupState,
        ILogger<TrainingSeriesGeneratorService> logger)
    {
        _scopeFactory = scopeFactory;
        _startupState = startupState;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The schedule below happens to delay the first pass past initialization, but that is a
        // property of the chosen run time, not a guarantee. Waiting explicitly keeps a future change
        // to that schedule from quietly reintroducing a race with the migration.
        await _startupState.WaitForReadyAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            // Target: 02:15 UTC daily
            var nextRun = now.Date.AddDays(now.Hour >= 2 && now.Minute >= 15 ? 1 : 0).AddHours(2).AddMinutes(15);
            var delay = nextRun - now;

            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, stoppingToken);

            if (stoppingToken.IsCancellationRequested)
                break;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var worker = scope.ServiceProvider.GetRequiredService<TrainingSeriesGeneratorWorker>();
                await worker.RunPassAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TrainingSeriesGeneratorService pass failed");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
