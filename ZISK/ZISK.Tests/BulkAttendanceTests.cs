using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Services;
using ZISK.Shared.DTOs.Attendance;
using SharedStatus = ZISK.Shared.Enums.AttendanceStatus;

namespace ZISK.Tests;

/// <summary>
/// AttendanceService.BulkMarkAttendanceAsync used to issue one FirstOrDefaultAsync per entry, so saving a
/// full team's attendance cost one database round-trip per child. It now pre-loads the training's existing
/// records into a dictionary. These tests pin the observable behaviour that refactor had to preserve
/// (update-existing vs insert-new), plus the duplicate-ChildId case, which previously produced two rows for
/// the same child and tripped the unique (TrainingEventId, ChildId) index as a raw DB error.
/// </summary>
public class BulkAttendanceTests
{
    private sealed class NoopAudit : IAuditService
    {
        public void Log(string action, string entityType, string entityId, ClaimsPrincipal? user, object? details = null) { }
    }

    private sealed class NoTeamAccess : ITeamAccessService
    {
        public Task<HashSet<Guid>?> GetAccessibleTeamIdsAsync(ClaimsPrincipal user) => Task.FromResult<HashSet<Guid>?>(null);
    }

    private static ClaimsPrincipal Coach(string id) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, id), new Claim(ClaimTypes.Role, "Coach")], "test"));

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

    private static ApplicationUser MakeUser(string first) => new()
    {
        Id = Guid.NewGuid().ToString(),
        UserName = first.ToLowerInvariant(),
        Email = $"{first.ToLowerInvariant()}@test.sk",
        FirstName = first,
        LastName = "Test",
        EmailConfirmed = true,
        IsActive = true
    };

    private static (TrainingEvent training, List<ApplicationUser> children, ApplicationUser coach) Seed(
        ApplicationDbContext db, int childCount)
    {
        var season = new Season
        {
            Id = Guid.NewGuid(), Name = "S", IsActive = true, CreatedAt = DateTime.UtcNow,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30))
        };
        var team = new Team { Id = Guid.NewGuid(), Name = $"T-{Guid.NewGuid():N}", CreatedAt = DateTime.UtcNow };
        var coach = MakeUser("Coach");
        var children = Enumerable.Range(0, childCount).Select(i => MakeUser($"Child{i}")).ToList();

        db.Seasons.Add(season);
        db.Teams.Add(team);
        db.Users.Add(coach);
        db.Users.AddRange(children);

        var training = new TrainingEvent
        {
            Id = Guid.NewGuid(), TeamId = team.Id, SeasonId = season.Id, Title = "Tréning",
            StartTime = DateTime.UtcNow.AddHours(-2), EndTime = DateTime.UtcNow.AddHours(-1),
            CreatedAt = DateTime.UtcNow
        };
        db.TrainingEvents.Add(training);
        db.SaveChanges();

        return (training, children, coach);
    }

    private static AttendanceService MakeService(ApplicationDbContext db)
        => new(db, new NoTeamAccess(), new NoopAudit());

    [Fact]
    public async Task InsertsRecords_ForEveryEntry()
    {
        var db = await BuildDbAsync(nameof(InsertsRecords_ForEveryEntry));
        var (training, children, coach) = Seed(db, 5);

        var request = new BulkMarkAttendanceRequest(
            training.Id,
            children.Select(c => new AttendanceEntryDto(c.Id, SharedStatus.Present, null)).ToList());

        await MakeService(db).BulkMarkAttendanceAsync(request, Coach(coach.Id));

        var records = await db.AttendanceRecords.Where(r => r.TrainingEventId == training.Id).ToListAsync();
        Assert.Equal(5, records.Count);
        Assert.All(records, r => Assert.Equal(AttendanceStatus.Present, r.Status));
    }

    [Fact]
    public async Task UpdatesExistingRecord_InsteadOfInsertingDuplicate()
    {
        var db = await BuildDbAsync(nameof(UpdatesExistingRecord_InsteadOfInsertingDuplicate));
        var (training, children, coach) = Seed(db, 3);
        var service = MakeService(db);

        var first = new BulkMarkAttendanceRequest(
            training.Id,
            children.Select(c => new AttendanceEntryDto(c.Id, SharedStatus.Present, "prvý zápis")).ToList());
        await service.BulkMarkAttendanceAsync(first, Coach(coach.Id));

        // Re-submit the same children with a different status — should update, not add.
        var second = new BulkMarkAttendanceRequest(
            training.Id,
            children.Select(c => new AttendanceEntryDto(c.Id, SharedStatus.Absent, "oprava")).ToList());
        await service.BulkMarkAttendanceAsync(second, Coach(coach.Id));

        var records = await db.AttendanceRecords.Where(r => r.TrainingEventId == training.Id).ToListAsync();
        Assert.Equal(3, records.Count);
        Assert.All(records, r =>
        {
            Assert.Equal(AttendanceStatus.Absent, r.Status);
            Assert.Equal("oprava", r.Note);
        });
    }

    [Fact]
    public async Task HandlesMixOfNewAndExistingEntries()
    {
        var db = await BuildDbAsync(nameof(HandlesMixOfNewAndExistingEntries));
        var (training, children, coach) = Seed(db, 4);
        var service = MakeService(db);

        // Mark only the first two.
        await service.BulkMarkAttendanceAsync(new BulkMarkAttendanceRequest(
            training.Id,
            children.Take(2).Select(c => new AttendanceEntryDto(c.Id, SharedStatus.Present, null)).ToList()),
            Coach(coach.Id));

        // Now submit all four: two updates + two inserts.
        await service.BulkMarkAttendanceAsync(new BulkMarkAttendanceRequest(
            training.Id,
            children.Select(c => new AttendanceEntryDto(c.Id, SharedStatus.Excused, null)).ToList()),
            Coach(coach.Id));

        var records = await db.AttendanceRecords.Where(r => r.TrainingEventId == training.Id).ToListAsync();
        Assert.Equal(4, records.Count);
        Assert.All(records, r => Assert.Equal(AttendanceStatus.Excused, r.Status));
        Assert.Equal(4, records.Select(r => r.ChildId).Distinct().Count());
    }

    [Fact]
    public async Task DuplicateChildIdInPayload_ProducesSingleRecord_LastWriteWins()
    {
        var db = await BuildDbAsync(nameof(DuplicateChildIdInPayload_ProducesSingleRecord_LastWriteWins));
        var (training, children, coach) = Seed(db, 1);
        var child = children[0];

        var request = new BulkMarkAttendanceRequest(training.Id,
        [
            new AttendanceEntryDto(child.Id, SharedStatus.Present, "prvý"),
            new AttendanceEntryDto(child.Id, SharedStatus.Absent, "druhý")
        ]);

        await MakeService(db).BulkMarkAttendanceAsync(request, Coach(coach.Id));

        var records = await db.AttendanceRecords.Where(r => r.TrainingEventId == training.Id).ToListAsync();
        Assert.Single(records);
        Assert.Equal(AttendanceStatus.Absent, records[0].Status);
        Assert.Equal("druhý", records[0].Note);
    }

    [Fact]
    public async Task ThrowsWhenTrainingIsLocked()
    {
        var db = await BuildDbAsync(nameof(ThrowsWhenTrainingIsLocked));
        var (training, children, coach) = Seed(db, 2);
        training.IsLocked = true;
        await db.SaveChangesAsync();

        var request = new BulkMarkAttendanceRequest(
            training.Id,
            children.Select(c => new AttendanceEntryDto(c.Id, SharedStatus.Present, null)).ToList());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => MakeService(db).BulkMarkAttendanceAsync(request, Coach(coach.Id)));
    }
}
