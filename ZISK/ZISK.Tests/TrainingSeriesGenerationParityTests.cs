using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Services;

namespace ZISK.Tests;

/// <summary>
/// The manual "Generate" action (TrainingSeriesService.GenerateInstancesAsync) and the nightly background
/// pass (TrainingSeriesGeneratorWorker.GenerateForSeriesAsync) each hand-rolled the same weekday-bitmask
/// expansion, and the two had drifted: only the worker clamped the requested window to the season's date
/// range, so an admin using the Generate button could create trainings outside the season.
///
/// Both now delegate to <see cref="TrainingSeriesInstanceGenerator"/>. These tests pin the clamp on the
/// service path (which never had it) and assert the two paths agree.
/// </summary>
public class TrainingSeriesGenerationParityTests
{
    private sealed class NoopAudit : IAuditService
    {
        public void Log(string action, string entityType, string entityId, ClaimsPrincipal? user, object? details = null) { }
    }

    private sealed class NoTeamAccess : ITeamAccessService
    {
        public Task<HashSet<Guid>?> GetAccessibleTeamIdsAsync(ClaimsPrincipal user) => Task.FromResult<HashSet<Guid>?>(null);
    }

    private static ClaimsPrincipal AdminUser() => new(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Admin")], "test"));

    private static async Task<ApplicationDbContext> BuildDbAsync(string dbName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static TrainingSeries SeedSeries(
        ApplicationDbContext db,
        DateOnly seasonStart,
        DateOnly seasonEnd,
        int daysOfWeek)
    {
        var season = new Season
        {
            Id = Guid.NewGuid(), Name = "Test", StartDate = seasonStart, EndDate = seasonEnd,
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var team = new Team { Id = Guid.NewGuid(), Name = $"Team-{Guid.NewGuid():N}", CreatedAt = DateTime.UtcNow };
        var coach = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(), UserName = "coach", Email = "coach@test.sk",
            FirstName = "Coach", LastName = "Test", EmailConfirmed = true
        };
        db.Seasons.Add(season);
        db.Teams.Add(team);
        db.Users.Add(coach);

        var series = new TrainingSeries
        {
            Id = Guid.NewGuid(), TeamId = team.Id, CoachId = coach.Id, SeasonId = season.Id,
            Title = "Test séria", DaysOfWeek = daysOfWeek,
            StartTime = new TimeOnly(17, 0), EndTime = new TimeOnly(18, 30),
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        db.TrainingSeries.Add(series);
        db.SaveChanges();
        db.Entry(series).Reference(s => s.Season).Load();
        return series;
    }

    private static TrainingSeriesService MakeService(ApplicationDbContext db)
        => new(db, new NoTeamAccess(), new NoopAudit());

    private static TrainingSeriesGeneratorWorker MakeWorker(ApplicationDbContext db)
        => new(db, new LoggerFactory().CreateLogger<TrainingSeriesGeneratorWorker>());

    private const int EveryDay = (int)(Weekdays.Monday | Weekdays.Tuesday | Weekdays.Wednesday
        | Weekdays.Thursday | Weekdays.Friday | Weekdays.Saturday | Weekdays.Sunday);

    [Fact]
    public async Task ManualGenerate_DoesNotCreateTrainings_PastSeasonEnd()
    {
        var db = await BuildDbAsync(nameof(ManualGenerate_DoesNotCreateTrainings_PastSeasonEnd));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var seasonEnd = today.AddDays(3);
        var series = SeedSeries(db, today, seasonEnd, EveryDay);

        // Ask for a window that overruns the season by 11 days.
        await MakeService(db).GenerateInstancesAsync(series.Id, today, today.AddDays(14), AdminUser());

        var events = await db.TrainingEvents.Where(te => te.SeriesId == series.Id).ToListAsync();

        Assert.NotEmpty(events);
        Assert.All(events, ev => Assert.True(
            DateOnly.FromDateTime(ev.StartTime) <= seasonEnd,
            $"Training generated on {ev.StartTime:d} — past season end {seasonEnd}"));
    }

    [Fact]
    public async Task ManualGenerate_DoesNotCreateTrainings_BeforeSeasonStart()
    {
        var db = await BuildDbAsync(nameof(ManualGenerate_DoesNotCreateTrainings_BeforeSeasonStart));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var seasonStart = today.AddDays(5);
        var series = SeedSeries(db, seasonStart, today.AddDays(30), EveryDay);

        await MakeService(db).GenerateInstancesAsync(series.Id, today, today.AddDays(20), AdminUser());

        var events = await db.TrainingEvents.Where(te => te.SeriesId == series.Id).ToListAsync();

        Assert.NotEmpty(events);
        Assert.All(events, ev => Assert.True(
            DateOnly.FromDateTime(ev.StartTime) >= seasonStart,
            $"Training generated on {ev.StartTime:d} — before season start {seasonStart}"));
    }

    [Fact]
    public async Task ManualGenerate_ReturnsCountMatchingRowsActuallyCreated()
    {
        var db = await BuildDbAsync(nameof(ManualGenerate_ReturnsCountMatchingRowsActuallyCreated));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var series = SeedSeries(db, today, today.AddDays(2), EveryDay);

        // The clamped window is 3 days, so the reported count must reflect the clamp, not the request.
        var reported = await MakeService(db).GenerateInstancesAsync(series.Id, today, today.AddDays(14), AdminUser());
        var actual = await db.TrainingEvents.CountAsync(te => te.SeriesId == series.Id);

        Assert.Equal(actual, reported);
        Assert.Equal(3, reported);
    }

    [Fact]
    public async Task ManualGenerate_IsIdempotent()
    {
        var db = await BuildDbAsync(nameof(ManualGenerate_IsIdempotent));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var series = SeedSeries(db, today, today.AddDays(20), EveryDay);
        var service = MakeService(db);

        await service.GenerateInstancesAsync(series.Id, today, today.AddDays(14), AdminUser());
        var afterFirst = await db.TrainingEvents.CountAsync(te => te.SeriesId == series.Id);

        var secondRunCount = await service.GenerateInstancesAsync(series.Id, today, today.AddDays(14), AdminUser());
        var afterSecond = await db.TrainingEvents.CountAsync(te => te.SeriesId == series.Id);

        Assert.Equal(afterFirst, afterSecond);
        Assert.Equal(0, secondRunCount);
    }

    [Fact]
    public async Task ManualAndBackgroundPaths_ProduceIdenticalDates()
    {
        var manualDb = await BuildDbAsync(nameof(ManualAndBackgroundPaths_ProduceIdenticalDates) + "-manual");
        var workerDb = await BuildDbAsync(nameof(ManualAndBackgroundPaths_ProduceIdenticalDates) + "-worker");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var mask = (int)(Weekdays.Monday | Weekdays.Wednesday | Weekdays.Friday);

        var manualSeries = SeedSeries(manualDb, today.AddDays(-2), today.AddDays(6), mask);
        var workerSeries = SeedSeries(workerDb, today.AddDays(-2), today.AddDays(6), mask);

        await MakeService(manualDb).GenerateInstancesAsync(manualSeries.Id, today, today.AddDays(14), AdminUser());
        await MakeWorker(workerDb).GenerateForSeriesAsync(workerSeries.Id, today, today.AddDays(14));

        var manualDates = await manualDb.TrainingEvents
            .Where(te => te.SeriesId == manualSeries.Id)
            .Select(te => te.StartTime)
            .ToListAsync();
        var workerDates = await workerDb.TrainingEvents
            .Where(te => te.SeriesId == workerSeries.Id)
            .Select(te => te.StartTime)
            .ToListAsync();

        Assert.NotEmpty(manualDates);
        Assert.Equal(
            workerDates.Select(d => d.ToString("s")).OrderBy(s => s),
            manualDates.Select(d => d.ToString("s")).OrderBy(s => s));
    }
}
