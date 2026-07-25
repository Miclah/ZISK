using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Services;

namespace ZISK.Tests;

public class AttendanceAutoCloseTests
{
    private sealed class NoopAudit : IAuditService
    {
        public void Log(string action, string entityType, string entityId, System.Security.Claims.ClaimsPrincipal? user, object? details) { }
    }

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

    private static AttendanceAutoCloseWorker MakeWorker(ApplicationDbContext db)
    {
        var attendanceService = new AttendanceService(db, new NoTeamAccess(), new NoopAudit());
        return new AttendanceAutoCloseWorker(db, attendanceService, new NoopAudit(),
            new LoggerFactory().CreateLogger<AttendanceAutoCloseWorker>());
    }

    private sealed class NoTeamAccess : ITeamAccessService
    {
        public Task<HashSet<Guid>?> GetAccessibleTeamIdsAsync(System.Security.Claims.ClaimsPrincipal user)
            => Task.FromResult<HashSet<Guid>?>(null);
    }

    private static ApplicationUser MakeUser(string suffix) => new()
    {
        Id = Guid.NewGuid().ToString(), UserName = $"u_{suffix}", Email = $"u_{suffix}@t.sk",
        FirstName = "Test", LastName = suffix, EmailConfirmed = true
    };

    private static Team MakeTeam(ApplicationDbContext db)
    {
        var team = new Team { Id = Guid.NewGuid(), Name = "T", CreatedAt = DateTime.UtcNow };
        db.Teams.Add(team);
        return team;
    }

    private static Season MakeSeason(ApplicationDbContext db)
    {
        var season = new Season
        {
            Id = Guid.NewGuid(), Name = "S",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-3)),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3)),
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        db.Seasons.Add(season);
        return season;
    }

    private static TrainingEvent MakePastTraining(ApplicationDbContext db, Team team, Season season, DateTime endTime)
    {
        var training = new TrainingEvent
        {
            Id = Guid.NewGuid(), TeamId = team.Id, SeasonId = season.Id,
            Title = "Test", StartTime = endTime.AddHours(-1.5), EndTime = endTime,
            IsLocked = false, IsCancelled = false, CreatedAt = DateTime.UtcNow
        };
        db.TrainingEvents.Add(training);
        return training;
    }

    [Fact]
    public async Task PastTraining_NoAttendance_AllMembersMarkedPresent_AndLocked()
    {
        var db = await BuildDbAsync(nameof(PastTraining_NoAttendance_AllMembersMarkedPresent_AndLocked));

        var team = MakeTeam(db);
        var season = MakeSeason(db);
        var past = DateTime.UtcNow.AddHours(-2);
        var training = MakePastTraining(db, team, season, past);

        var member1 = MakeUser("m1");
        var member2 = MakeUser("m2");
        db.Users.AddRange(member1, member2);
        db.TeamMembers.Add(new TeamMember { TeamId = team.Id, UserId = member1.Id, JoinedAt = past.AddDays(-30) });
        db.TeamMembers.Add(new TeamMember { TeamId = team.Id, UserId = member2.Id, JoinedAt = past.AddDays(-30) });
        await db.SaveChangesAsync();

        await MakeWorker(db).RunPassAsync();

        var records = await db.AttendanceRecords.Where(r => r.TrainingEventId == training.Id).ToListAsync();
        Assert.Equal(2, records.Count);
        Assert.All(records, r => Assert.Equal(AttendanceStatus.Present, r.Status));

        var locked = await db.TrainingEvents.Where(t => t.Id == training.Id).Select(t => t.IsLocked).FirstAsync();
        Assert.True(locked);
    }

    [Fact]
    public async Task PastTraining_PartialAttendance_OnlyMissingAreAdded()
    {
        var db = await BuildDbAsync(nameof(PastTraining_PartialAttendance_OnlyMissingAreAdded));

        var team = MakeTeam(db);
        var season = MakeSeason(db);
        var past = DateTime.UtcNow.AddHours(-2);
        var training = MakePastTraining(db, team, season, past);

        var member1 = MakeUser("m1");
        var member2 = MakeUser("m2");
        db.Users.AddRange(member1, member2);
        db.TeamMembers.Add(new TeamMember { TeamId = team.Id, UserId = member1.Id, JoinedAt = past.AddDays(-30) });
        db.TeamMembers.Add(new TeamMember { TeamId = team.Id, UserId = member2.Id, JoinedAt = past.AddDays(-30) });

        // member1 already has a manual Absent record
        db.AttendanceRecords.Add(new AttendanceRecord
        {
            Id = Guid.NewGuid(), TrainingEventId = training.Id, ChildId = member1.Id,
            Status = AttendanceStatus.Absent, RecordedAt = DateTime.UtcNow.AddMinutes(-30)
        });
        await db.SaveChangesAsync();

        await MakeWorker(db).RunPassAsync();

        var records = await db.AttendanceRecords.Where(r => r.TrainingEventId == training.Id).ToListAsync();
        Assert.Equal(2, records.Count);

        var m1Record = records.First(r => r.ChildId == member1.Id);
        Assert.Equal(AttendanceStatus.Absent, m1Record.Status); // unchanged

        var m2Record = records.First(r => r.ChildId == member2.Id);
        Assert.Equal(AttendanceStatus.Present, m2Record.Status); // auto-added
    }

    [Fact]
    public async Task MemberJoinedAfterTraining_NoRetroactiveRecord()
    {
        var db = await BuildDbAsync(nameof(MemberJoinedAfterTraining_NoRetroactiveRecord));

        var team = MakeTeam(db);
        var season = MakeSeason(db);
        var past = DateTime.UtcNow.AddHours(-2);
        var training = MakePastTraining(db, team, season, past);

        var lateJoiner = MakeUser("late");
        db.Users.Add(lateJoiner);
        // JoinedAt is AFTER the training ended
        db.TeamMembers.Add(new TeamMember { TeamId = team.Id, UserId = lateJoiner.Id, JoinedAt = past.AddHours(1) });
        await db.SaveChangesAsync();

        await MakeWorker(db).RunPassAsync();

        var count = await db.AttendanceRecords.CountAsync(r => r.TrainingEventId == training.Id);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task AbsenceRequest_OverridesToExcused()
    {
        var db = await BuildDbAsync(nameof(AbsenceRequest_OverridesToExcused));

        var team = MakeTeam(db);
        var season = MakeSeason(db);
        var past = DateTime.UtcNow.AddHours(-2);
        var training = MakePastTraining(db, team, season, past);

        var member = MakeUser("excused");
        var parent = MakeUser("parent");
        db.Users.AddRange(member, parent);
        db.TeamMembers.Add(new TeamMember { TeamId = team.Id, UserId = member.Id, JoinedAt = past.AddDays(-10) });
        db.AbsenceRequests.Add(new AbsenceRequest
        {
            Id = Guid.NewGuid(), ChildId = member.Id, ParentId = parent.Id,
            TrainingEventId = training.Id, Status = AbsenceRequestStatus.Received,
            CreatedAt = DateTime.UtcNow.AddHours(-3)
        });
        await db.SaveChangesAsync();

        await MakeWorker(db).RunPassAsync();

        var record = await db.AttendanceRecords.FirstAsync(r => r.TrainingEventId == training.Id && r.ChildId == member.Id);
        Assert.Equal(AttendanceStatus.Excused, record.Status);
    }

    [Fact]
    public async Task AlreadyLockedTraining_IsSkipped()
    {
        var db = await BuildDbAsync(nameof(AlreadyLockedTraining_IsSkipped));

        var team = MakeTeam(db);
        var season = MakeSeason(db);
        var past = DateTime.UtcNow.AddHours(-2);
        var training = MakePastTraining(db, team, season, past);
        training.IsLocked = true; // already locked

        var member = MakeUser("m");
        db.Users.Add(member);
        db.TeamMembers.Add(new TeamMember { TeamId = team.Id, UserId = member.Id, JoinedAt = past.AddDays(-10) });
        await db.SaveChangesAsync();

        await MakeWorker(db).RunPassAsync();

        var count = await db.AttendanceRecords.CountAsync(r => r.TrainingEventId == training.Id);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task CancelledTraining_IsSkipped()
    {
        var db = await BuildDbAsync(nameof(CancelledTraining_IsSkipped));

        var team = MakeTeam(db);
        var season = MakeSeason(db);
        var past = DateTime.UtcNow.AddHours(-2);
        var training = MakePastTraining(db, team, season, past);
        training.IsCancelled = true;

        var member = MakeUser("m");
        db.Users.Add(member);
        db.TeamMembers.Add(new TeamMember { TeamId = team.Id, UserId = member.Id, JoinedAt = past.AddDays(-10) });
        await db.SaveChangesAsync();

        await MakeWorker(db).RunPassAsync();

        var count = await db.AttendanceRecords.CountAsync(r => r.TrainingEventId == training.Id);
        Assert.Equal(0, count);
    }
}
