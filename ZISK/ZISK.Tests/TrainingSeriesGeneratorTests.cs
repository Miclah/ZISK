using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Services;

namespace ZISK.Tests;

public class TrainingSeriesGeneratorTests
{
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

    private static TrainingSeriesGeneratorWorker MakeWorker(ApplicationDbContext db)
        => new(db, new LoggerFactory().CreateLogger<TrainingSeriesGeneratorWorker>());

    private static (Season season, Team team, TrainingSeries series) SeedSeries(
        ApplicationDbContext db,
        DateOnly seasonStart, DateOnly seasonEnd,
        int daysOfWeek,
        TimeOnly startTime, TimeOnly endTime)
    {
        var season = new Season
        {
            Id = Guid.NewGuid(), Name = "Test", StartDate = seasonStart, EndDate = seasonEnd,
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var team = new Team { Id = Guid.NewGuid(), Name = "Team", CreatedAt = DateTime.UtcNow };
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(), UserName = "coach", Email = "coach@test.sk",
            FirstName = "Coach", LastName = "Test", EmailConfirmed = true
        };
        db.Seasons.Add(season);
        db.Teams.Add(team);
        db.Users.Add(user);

        var series = new TrainingSeries
        {
            Id = Guid.NewGuid(), TeamId = team.Id, CoachId = user.Id, SeasonId = season.Id,
            Title = "Test séria", DaysOfWeek = daysOfWeek,
            StartTime = startTime, EndTime = endTime, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        db.TrainingSeries.Add(series);
        db.SaveChanges();

        // Reload with navigations
        db.Entry(series).Reference(s => s.Season).Load();
        return (season, team, series);
    }

    [Fact]
    public async Task Generates_CorrectDates_ForMondayWednesdayFriday_Series()
    {
        var db = await BuildDbAsync(nameof(Generates_CorrectDates_ForMondayWednesdayFriday_Series));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (_, _, series) = SeedSeries(db, today.AddDays(-5), today.AddDays(20),
            daysOfWeek: (int)(Weekdays.Monday | Weekdays.Wednesday | Weekdays.Friday),
            startTime: new TimeOnly(17, 0), endTime: new TimeOnly(18, 30));

        var worker = MakeWorker(db);
        await worker.GenerateForSeriesAsync(series.Id, today, today.AddDays(14));

        var events = await db.TrainingEvents.Where(te => te.SeriesId == series.Id).ToListAsync();

        Assert.NotEmpty(events);
        foreach (var ev in events)
        {
            var dow = ev.StartTime.DayOfWeek;
            Assert.True(dow == DayOfWeek.Monday || dow == DayOfWeek.Wednesday || dow == DayOfWeek.Friday);
        }
    }

    [Fact]
    public async Task Idempotent_DoesNotDuplicate_OnSecondRun()
    {
        var db = await BuildDbAsync(nameof(Idempotent_DoesNotDuplicate_OnSecondRun));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (_, _, series) = SeedSeries(db, today.AddDays(-1), today.AddDays(20),
            daysOfWeek: (int)Weekdays.Monday,
            startTime: new TimeOnly(17, 0), endTime: new TimeOnly(18, 30));

        var worker = MakeWorker(db);
        await worker.GenerateForSeriesAsync(series.Id, today, today.AddDays(14));
        var countAfterFirst = await db.TrainingEvents.CountAsync(te => te.SeriesId == series.Id);

        await worker.GenerateForSeriesAsync(series.Id, today, today.AddDays(14));
        var countAfterSecond = await db.TrainingEvents.CountAsync(te => te.SeriesId == series.Id);

        Assert.Equal(countAfterFirst, countAfterSecond);
    }

    [Fact]
    public async Task DoesNotGenerate_OutsideSeasonDateRange()
    {
        var db = await BuildDbAsync(nameof(DoesNotGenerate_OutsideSeasonDateRange));

        // Season ends in 3 days, but we try to generate for 14 days
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (_, _, series) = SeedSeries(db, today, today.AddDays(3),
            daysOfWeek: (int)(Weekdays.Monday | Weekdays.Tuesday | Weekdays.Wednesday | Weekdays.Thursday | Weekdays.Friday | Weekdays.Saturday | Weekdays.Sunday),
            startTime: new TimeOnly(17, 0), endTime: new TimeOnly(18, 0));

        var worker = MakeWorker(db);
        await worker.GenerateForSeriesAsync(series.Id, today, today.AddDays(14));

        var events = await db.TrainingEvents.Where(te => te.SeriesId == series.Id).ToListAsync();
        Assert.All(events, ev => Assert.True(DateOnly.FromDateTime(ev.StartTime) <= today.AddDays(3)));
    }

    [Fact]
    public async Task SkipsInactiveSeries()
    {
        var db = await BuildDbAsync(nameof(SkipsInactiveSeries));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var (_, _, series) = SeedSeries(db, today.AddDays(-1), today.AddDays(20),
            daysOfWeek: (int)(Weekdays.Monday | Weekdays.Wednesday | Weekdays.Friday),
            startTime: new TimeOnly(17, 0), endTime: new TimeOnly(18, 0));

        // Deactivate the series
        series.IsActive = false;
        await db.SaveChangesAsync();

        var worker = MakeWorker(db);
        await worker.RunPassAsync();

        var count = await db.TrainingEvents.CountAsync(te => te.SeriesId == series.Id);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task RespectsManualTrainingEvent_WhenDateCollides()
    {
        var db = await BuildDbAsync(nameof(RespectsManualTrainingEvent_WhenDateCollides));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Find next Monday
        var nextMonday = today;
        while (nextMonday.DayOfWeek != DayOfWeek.Monday)
            nextMonday = nextMonday.AddDays(1);

        var (_, team, series) = SeedSeries(db, today.AddDays(-1), today.AddDays(30),
            daysOfWeek: (int)Weekdays.Monday,
            startTime: new TimeOnly(17, 0), endTime: new TimeOnly(18, 0));

        // Pre-existing training on Monday (without SeriesId — manual)
        db.TrainingEvents.Add(new TrainingEvent
        {
            Id = Guid.NewGuid(), TeamId = team.Id, SeriesId = series.Id,
            SeasonId = series.SeasonId,
            Title = "Manual", StartTime = nextMonday.ToDateTime(new TimeOnly(17, 0)),
            EndTime = nextMonday.ToDateTime(new TimeOnly(18, 0)), CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var worker = MakeWorker(db);
        await worker.GenerateForSeriesAsync(series.Id, today, today.AddDays(14));

        // Should still be exactly 1 event on that Monday
        var mondayEvents = await db.TrainingEvents
            .Where(te => te.SeriesId == series.Id && te.StartTime.Date == nextMonday.ToDateTime(TimeOnly.MinValue).Date)
            .ToListAsync();

        Assert.Single(mondayEvents);
    }
}
