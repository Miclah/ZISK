using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Services;
using ZISK.Services.Demo;

namespace ZISK.Tests;

/// <summary>
/// Regression tests for the public-demo per-visitor isolation (Fáza 6: ZISK_SEED_MODE=demo).
/// Covers the two things that must never break: cloning a session must not leak into another
/// session or back into the shared template, and reset/cleanup must not leave orphaned rows
/// behind for entities with Restrict-delete foreign keys.
/// </summary>
public class DemoSessionServiceTests
{
    private sealed class NoopFileService : IFileService
    {
        public (bool IsValid, string? ErrorMessage) ValidateFile(IFormFile file, long maxSizeBytes, string[] allowedExtensions) => (true, null);
        public Task<string> SaveFileAsync(IFormFile file, string subfolder) => Task.FromResult("");
        public void DeleteFile(string relativePath) { }
        public string GetContentType(string filePath) => "application/octet-stream";
    }

    private static async Task<(ApplicationDbContext Db, DemoSessionService Sessions)> BuildAsync(string dbName, int maxActiveSessions = 200)
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();

        var options = Options.Create(new DemoOptions { MaxActiveSessions = maxActiveSessions });
        var sessions = new DemoSessionService(db, new NoopFileService(), options, NullLogger());
        return (db, sessions);
    }

    private static ILogger<DemoSessionService> NullLogger() =>
        new LoggerFactory().CreateLogger<DemoSessionService>();

    /// <summary>Seeds a minimal template: one season, one team, one admin, one training, one attendance record, one announcement.</summary>
    private static async Task<(Team Team, ApplicationUser Admin, TrainingEvent Training)> SeedTemplateAsync(ApplicationDbContext db)
    {
        var season = new Season { Id = Guid.NewGuid(), Name = "Test Season", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), IsActive = true };
        var team = new Team { Id = Guid.NewGuid(), Name = "A-tím", ShortName = "A", IsActive = true };
        var admin = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "admin@zisk.sk",
            NormalizedUserName = "ADMIN@ZISK.SK",
            Email = "admin@zisk.sk",
            NormalizedEmail = "ADMIN@ZISK.SK",
            PasswordHash = "hash",
            FirstName = "Miroslav",
            LastName = "Kráľ"
        };
        var training = new TrainingEvent
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            SeasonId = season.Id,
            Title = "Tréning",
            StartTime = DateTime.UtcNow.AddDays(-1),
            EndTime = DateTime.UtcNow.AddDays(-1).AddHours(1)
        };
        var attendance = new AttendanceRecord
        {
            Id = Guid.NewGuid(),
            TrainingEventId = training.Id,
            ChildId = admin.Id,
            Status = AttendanceStatus.Present
        };
        var announcement = new Announcement
        {
            Id = Guid.NewGuid(),
            Title = "Test",
            Content = "Content",
            AuthorUserId = admin.Id,
            TargetAudience = TargetAudience.All,
            Priority = AnnouncementPriority.Low
        };

        db.Seasons.Add(season);
        db.Teams.Add(team);
        db.Users.Add(admin);
        db.TeamMembers.Add(new TeamMember { TeamId = team.Id, UserId = admin.Id });
        db.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { UserId = admin.Id, RoleId = "admin-role-id" });
        db.TrainingEvents.Add(training);
        db.AttendanceRecords.Add(attendance);
        db.Announcements.Add(announcement);
        await db.SaveChangesAsync();

        return (team, admin, training);
    }

    [Fact]
    public async Task CloneTemplateIntoNewSession_CopiesRowsWithNewIds_NotSharedWithTemplate()
    {
        var (db, sessions) = await BuildAsync(nameof(CloneTemplateIntoNewSession_CopiesRowsWithNewIds_NotSharedWithTemplate));
        var (team, admin, training) = await SeedTemplateAsync(db);

        var sessionId = await sessions.EnsureSessionAsync(null, "Admin");

        var clonedTeams = await db.Teams.IgnoreQueryFilters().Where(t => t.DemoSessionId == sessionId).ToListAsync();
        var clonedUsers = await db.Users.IgnoreQueryFilters().Where(u => u.DemoSessionId == sessionId).ToListAsync();
        var clonedTrainings = await db.TrainingEvents.IgnoreQueryFilters().Where(t => t.DemoSessionId == sessionId).ToListAsync();
        var clonedAttendance = await db.AttendanceRecords.IgnoreQueryFilters().Where(a => a.DemoSessionId == sessionId).ToListAsync();
        var clonedAnnouncements = await db.Announcements.IgnoreQueryFilters().Where(a => a.DemoSessionId == sessionId).ToListAsync();

        Assert.Single(clonedTeams);
        Assert.NotEqual(team.Id, clonedTeams[0].Id);
        Assert.Equal(team.Name, clonedTeams[0].Name);

        Assert.Single(clonedUsers);
        Assert.NotEqual(admin.Id, clonedUsers[0].Id);
        Assert.StartsWith(DemoSessionService.Prefix(sessionId), clonedUsers[0].UserName);
        Assert.EndsWith("admin@zisk.sk", clonedUsers[0].UserName);
        Assert.Equal(admin.PasswordHash, clonedUsers[0].PasswordHash);

        Assert.Single(clonedTrainings);
        Assert.NotEqual(training.Id, clonedTrainings[0].Id);
        Assert.Equal(clonedTeams[0].Id, clonedTrainings[0].TeamId);

        Assert.Single(clonedAttendance);
        Assert.Equal(clonedTrainings[0].Id, clonedAttendance[0].TrainingEventId);
        Assert.Equal(clonedUsers[0].Id, clonedAttendance[0].ChildId);

        Assert.Single(clonedAnnouncements);
        Assert.Equal(clonedUsers[0].Id, clonedAnnouncements[0].AuthorUserId);

        // Template itself must be untouched.
        var templateTeams = await db.Teams.IgnoreQueryFilters().Where(t => t.DemoSessionId == null).ToListAsync();
        Assert.Single(templateTeams);
        Assert.Equal(team.Id, templateTeams[0].Id);
    }

    [Fact]
    public async Task TwoSessions_ClonedFromSameTemplate_AreFullyIsolatedFromEachOther()
    {
        var (db, sessions) = await BuildAsync(nameof(TwoSessions_ClonedFromSameTemplate_AreFullyIsolatedFromEachOther));
        await SeedTemplateAsync(db);

        var sessionA = await sessions.EnsureSessionAsync(null, "Admin");
        var sessionB = await sessions.EnsureSessionAsync(null, "Admin");

        Assert.NotEqual(sessionA, sessionB);

        var teamsA = await db.Teams.IgnoreQueryFilters().Where(t => t.DemoSessionId == sessionA).ToListAsync();
        var teamsB = await db.Teams.IgnoreQueryFilters().Where(t => t.DemoSessionId == sessionB).ToListAsync();

        Assert.Single(teamsA);
        Assert.Single(teamsB);
        Assert.NotEqual(teamsA[0].Id, teamsB[0].Id);

        // Deleting session A's team must not touch session B's clone or the template.
        db.Teams.Remove(teamsA[0]);
        await db.SaveChangesAsync();

        var remainingB = await db.Teams.IgnoreQueryFilters().Where(t => t.DemoSessionId == sessionB).ToListAsync();
        var remainingTemplate = await db.Teams.IgnoreQueryFilters().Where(t => t.DemoSessionId == null).ToListAsync();
        Assert.Single(remainingB);
        Assert.Single(remainingTemplate);
    }

    [Fact]
    public async Task GetUserForRoleAsync_FindsTheClonedAdmin()
    {
        var (db, sessions) = await BuildAsync(nameof(GetUserForRoleAsync_FindsTheClonedAdmin));
        await SeedTemplateAsync(db);

        var sessionId = await sessions.EnsureSessionAsync(null, "Admin");
        var clonedAdmin = await sessions.GetUserForRoleAsync(sessionId, "Admin");

        Assert.NotNull(clonedAdmin);
        Assert.Equal(sessionId, clonedAdmin!.DemoSessionId);
    }

    [Fact]
    public async Task EnsureSessionAsync_WithExistingCookie_ReturnsSameSessionAndTouchesIt()
    {
        var (db, sessions) = await BuildAsync(nameof(EnsureSessionAsync_WithExistingCookie_ReturnsSameSessionAndTouchesIt));
        await SeedTemplateAsync(db);

        var sessionId = await sessions.EnsureSessionAsync(null, "Admin");
        var before = await db.DemoSessions.SingleAsync(s => s.Id == sessionId);
        var beforeLastSeen = before.LastSeenAt;

        await Task.Delay(10);
        var again = await sessions.EnsureSessionAsync(sessionId);

        Assert.Equal(sessionId, again);
        var teams = await db.Teams.IgnoreQueryFilters().Where(t => t.DemoSessionId == sessionId).ToListAsync();
        Assert.Single(teams); // not re-cloned

        var after = await db.DemoSessions.SingleAsync(s => s.Id == sessionId);
        Assert.True(after.LastSeenAt >= beforeLastSeen);
    }

    [Fact]
    public async Task DeleteSessionDataAsync_RemovesEverythingIncludingUserRoles()
    {
        var (db, sessions) = await BuildAsync(nameof(DeleteSessionDataAsync_RemovesEverythingIncludingUserRoles));
        await SeedTemplateAsync(db);

        var sessionId = await sessions.EnsureSessionAsync(null, "Admin");
        var clonedUserId = (await db.Users.IgnoreQueryFilters().FirstAsync(u => u.DemoSessionId == sessionId)).Id;

        await sessions.DeleteSessionDataAsync(sessionId, alsoDeleteSessionRow: true);

        Assert.False(await db.Teams.IgnoreQueryFilters().AnyAsync(t => t.DemoSessionId == sessionId));
        Assert.False(await db.Users.IgnoreQueryFilters().AnyAsync(u => u.DemoSessionId == sessionId));
        Assert.False(await db.TrainingEvents.IgnoreQueryFilters().AnyAsync(t => t.DemoSessionId == sessionId));
        Assert.False(await db.AttendanceRecords.IgnoreQueryFilters().AnyAsync(a => a.DemoSessionId == sessionId));
        Assert.False(await db.Announcements.IgnoreQueryFilters().AnyAsync(a => a.DemoSessionId == sessionId));
        Assert.False(await db.UserRoles.AnyAsync(ur => ur.UserId == clonedUserId));
        Assert.False(await db.DemoSessions.AnyAsync(s => s.Id == sessionId));

        // Template must be completely untouched by deleting a clone.
        Assert.True(await db.Teams.IgnoreQueryFilters().AnyAsync(t => t.DemoSessionId == null));
    }

    [Fact]
    public async Task ResetSessionAsync_ReplacesDataUnderANewSessionId()
    {
        var (db, sessions) = await BuildAsync(nameof(ResetSessionAsync_ReplacesDataUnderANewSessionId));
        await SeedTemplateAsync(db);

        var originalSessionId = await sessions.EnsureSessionAsync(null, "Admin");
        var originalTeamId = (await db.Teams.IgnoreQueryFilters().FirstAsync(t => t.DemoSessionId == originalSessionId)).Id;

        var newSessionId = await sessions.ResetSessionAsync(originalSessionId);

        Assert.NotEqual(originalSessionId, newSessionId);
        Assert.False(await db.DemoSessions.AnyAsync(s => s.Id == originalSessionId));
        Assert.False(await db.Teams.IgnoreQueryFilters().AnyAsync(t => t.Id == originalTeamId));

        var newTeams = await db.Teams.IgnoreQueryFilters().Where(t => t.DemoSessionId == newSessionId).ToListAsync();
        Assert.Single(newTeams);
    }

    [Fact]
    public async Task EnforceSessionCap_EvictsOldestSessionWhenCapReached()
    {
        var (db, sessions) = await BuildAsync(nameof(EnforceSessionCap_EvictsOldestSessionWhenCapReached), maxActiveSessions: 1);
        await SeedTemplateAsync(db);

        var firstSessionId = await sessions.EnsureSessionAsync(null, "Admin");
        // Force the first session to look older than whatever gets created next.
        var firstSession = await db.DemoSessions.SingleAsync(s => s.Id == firstSessionId);
        firstSession.LastSeenAt = DateTime.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();

        var secondSessionId = await sessions.EnsureSessionAsync(null, "Admin");

        Assert.False(await db.DemoSessions.AnyAsync(s => s.Id == firstSessionId));
        Assert.True(await db.DemoSessions.AnyAsync(s => s.Id == secondSessionId));
    }
}
