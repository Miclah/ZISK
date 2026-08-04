using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Services;
using ZISK.Shared.DTOs.Trainings;

namespace ZISK.Tests;

public class TrainingCancellationTests
{
    // ── Fakes ───────────────────────────────────────────────────────────────

    private sealed class NoopAudit : IAuditService
    {
        public void Log(string action, string entityType, string entityId,
            ClaimsPrincipal? user, object? details = null) { }
    }

    private sealed class NoTeamAccess : ITeamAccessService
    {
        public Task<HashSet<Guid>?> GetAccessibleTeamIdsAsync(ClaimsPrincipal user)
            => Task.FromResult<HashSet<Guid>?>(null);
    }

    private sealed class RestrictedTeamAccess : ITeamAccessService
    {
        private readonly HashSet<Guid> _allowed;
        public RestrictedTeamAccess(params Guid[] allowed) => _allowed = [.. allowed];
        public Task<HashSet<Guid>?> GetAccessibleTeamIdsAsync(ClaimsPrincipal user)
            => Task.FromResult<HashSet<Guid>?>(_allowed);
    }

    private sealed class FakeEmailSender : Microsoft.AspNetCore.Identity.UI.Services.IEmailSender
    {
        public List<(string To, string Subject)> Sent { get; } = [];
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            Sent.Add((email, subject));
            return Task.CompletedTask;
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

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

    private static TrainingService MakeService(
        ApplicationDbContext db,
        ITeamAccessService? teamAccess = null,
        FakeEmailSender? emailSender = null)
        => new(
            db,
            teamAccess ?? new NoTeamAccess(),
            new NoopAudit(),
            emailSender ?? new FakeEmailSender(),
            new LoggerFactory().CreateLogger<TrainingService>(),
            new FakeCurrentLanguage());

    private static ApplicationUser MakeUser(string suffix, string? email = null) => new()
    {
        Id = Guid.NewGuid().ToString(),
        UserName = $"u_{suffix}",
        Email = email ?? $"u_{suffix}@test.sk",
        FirstName = "Test", LastName = suffix, EmailConfirmed = true
    };

    private static Team MakeTeam(ApplicationDbContext db)
    {
        var team = new Team { Id = Guid.NewGuid(), Name = "TestTeam", CreatedAt = DateTime.UtcNow };
        db.Teams.Add(team);
        return team;
    }

    private static TrainingEvent MakeTraining(ApplicationDbContext db, Team team, DateTime? start = null)
    {
        var s = start ?? DateTime.UtcNow.AddDays(1);
        var te = new TrainingEvent
        {
            Id = Guid.NewGuid(), TeamId = team.Id, SeasonId = Guid.NewGuid(),
            Title = "Test Training", StartTime = s, EndTime = s.AddHours(1.5),
            CreatedAt = DateTime.UtcNow
        };
        db.TrainingEvents.Add(te);
        return te;
    }

    private static ClaimsPrincipal AdminUser() =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Admin")], "test"));

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cancel_SetsIsCancelledAndReason()
    {
        var db = await BuildDbAsync(nameof(Cancel_SetsIsCancelledAndReason));
        var team = MakeTeam(db);
        var training = MakeTraining(db, team);
        await db.SaveChangesAsync();

        var svc = MakeService(db);
        await svc.CancelTrainingAsync(training.Id, new CancelTrainingRequest("Dôvod"), AdminUser());

        var updated = await db.TrainingEvents.FindAsync(training.Id);
        Assert.NotNull(updated);
        Assert.True(updated.IsCancelled);
        Assert.Equal("Dôvod", updated.CancelledReason);
    }

    [Fact]
    public async Task Cancel_SendsEmailToAllTeamMembersAndParents()
    {
        var db = await BuildDbAsync(nameof(Cancel_SendsEmailToAllTeamMembersAndParents));
        var team = MakeTeam(db);
        var training = MakeTraining(db, team);

        var member1 = MakeUser("m1", "member1@test.sk");
        var member2 = MakeUser("m2", "member2@test.sk");
        var parent1 = MakeUser("p1", "parent1@test.sk");
        db.Users.AddRange(member1, member2, parent1);

        db.TeamMembers.AddRange(
            new TeamMember { TeamId = team.Id, UserId = member1.Id, JoinedAt = DateTime.UtcNow.AddDays(-10) },
            new TeamMember { TeamId = team.Id, UserId = member2.Id, JoinedAt = DateTime.UtcNow.AddDays(-10) });

        db.ParentChildren.Add(new ParentChild
        {
            ParentId = parent1.Id, ChildId = member1.Id, IsPrimary = true
        });

        await db.SaveChangesAsync();

        var emailSender = new FakeEmailSender();
        var svc = MakeService(db, emailSender: emailSender);
        await svc.CancelTrainingAsync(training.Id, new CancelTrainingRequest("Dôvod"), AdminUser());

        var sentTo = emailSender.Sent.Select(s => s.To).ToHashSet();
        Assert.Contains("member1@test.sk", sentTo);
        Assert.Contains("member2@test.sk", sentTo);
        Assert.Contains("parent1@test.sk", sentTo);
        Assert.Equal(3, sentTo.Count);
    }

    [Fact]
    public async Task Cancel_ForbidsCoachFromOtherTeam()
    {
        var db = await BuildDbAsync(nameof(Cancel_ForbidsCoachFromOtherTeam));
        var team = MakeTeam(db);
        var training = MakeTraining(db, team);
        await db.SaveChangesAsync();

        // Coach has access only to a different team
        var otherTeamId = Guid.NewGuid();
        var svc = MakeService(db, teamAccess: new RestrictedTeamAccess(otherTeamId));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.CancelTrainingAsync(training.Id, new CancelTrainingRequest("x"), AdminUser()));
    }

    [Fact]
    public async Task GetTrainings_RangeFilter_ReturnsOnlyOverlapping()
    {
        var db = await BuildDbAsync(nameof(GetTrainings_RangeFilter_ReturnsOnlyOverlapping));
        var team = MakeTeam(db);

        var inside = MakeTraining(db, team, DateTime.UtcNow.AddDays(3));
        MakeTraining(db, team, DateTime.UtcNow.AddDays(30));  // outside
        await db.SaveChangesAsync();

        var svc = MakeService(db);
        var result = await svc.GetTrainingsAsync(
            null,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(7),
            AdminUser());

        Assert.Single(result);
        Assert.Equal(inside.Id, result[0].Id);
    }

    [Fact]
    public async Task GetTrainings_CancelledFlag_IsMappedToDto()
    {
        var db = await BuildDbAsync(nameof(GetTrainings_CancelledFlag_IsMappedToDto));
        var team = MakeTeam(db);
        var training = MakeTraining(db, team);
        training.IsCancelled = true;
        training.CancelledReason = "Test reason";
        await db.SaveChangesAsync();

        var svc = MakeService(db);
        var result = await svc.GetTrainingsAsync(null, null, null, AdminUser());

        Assert.Single(result);
        Assert.True(result[0].IsCancelled);
        Assert.Equal("Test reason", result[0].CancelledReason);
    }
}
