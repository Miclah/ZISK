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

        // Clamp range to season boundaries
        var seasonStart = series.Season.StartDate;
        var seasonEnd = series.Season.EndDate;
        var effectiveFrom = from < seasonStart ? seasonStart : from;
        var effectiveTo = to > seasonEnd ? seasonEnd : to;

        if (effectiveFrom > effectiveTo)
            return;

        var weekdays = (Weekdays)series.DaysOfWeek;

        var existingDates = await _context.TrainingEvents
            .Where(te => te.SeriesId == seriesId)
            .Select(te => DateOnly.FromDateTime(te.StartTime))
            .ToListAsync(ct);

        var existingSet = new HashSet<DateOnly>(existingDates);
        var generated = 0;

        for (var date = effectiveFrom; date <= effectiveTo; date = date.AddDays(1))
        {
            var dayFlag = date.DayOfWeek switch
            {
                DayOfWeek.Monday => Weekdays.Monday,
                DayOfWeek.Tuesday => Weekdays.Tuesday,
                DayOfWeek.Wednesday => Weekdays.Wednesday,
                DayOfWeek.Thursday => Weekdays.Thursday,
                DayOfWeek.Friday => Weekdays.Friday,
                DayOfWeek.Saturday => Weekdays.Saturday,
                DayOfWeek.Sunday => Weekdays.Sunday,
                _ => Weekdays.None
            };

            if ((weekdays & dayFlag) == Weekdays.None || existingSet.Contains(date))
                continue;

            _context.TrainingEvents.Add(new TrainingEvent
            {
                Id = Guid.NewGuid(),
                TeamId = series.TeamId,
                SeriesId = series.Id,
                SeasonId = series.SeasonId,
                Title = series.Title,
                StartTime = date.ToDateTime(series.StartTime),
                EndTime = date.ToDateTime(series.EndTime),
                Location = series.Location,
                Type = series.Type,
                CoachNote = series.CoachNote,
                CreatedAt = DateTime.UtcNow
            });

            existingSet.Add(date);
            generated++;
        }

        if (generated > 0)
            await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Generated {Count} training instances for series {SeriesId}", generated, seriesId);
    }

    public async Task RunPassAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizon = today.AddDays(14);

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
    private readonly ILogger<TrainingSeriesGeneratorService> _logger;

    public TrainingSeriesGeneratorService(IServiceScopeFactory scopeFactory, ILogger<TrainingSeriesGeneratorService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
