using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Services;
using ZISK.Shared.DTOs.Trainings;

namespace ZISK.Tests;

/// <summary>
/// Delete operations are the easiest place to get a restrict-vs-cascade relationship wrong,
/// and the failure is ugly: either a raw foreign-key exception reaches the user instead of a
/// readable message, or a dependent row disappears without anyone asking for it. These tests
/// pin down the delete path for users, teams, seasons and training series, including the
/// team-scope check a coach has to pass before deleting a series.
/// </summary>
public class DeletePathFixesTests
{
    private sealed class NoopAudit : IAuditService
    {
        public void Log(string action, string entityType, string entityId, ClaimsPrincipal? user, object? details = null) { }
    }

    private sealed class NoTeamAccess : ITeamAccessService
    {
        public Task<HashSet<Guid>?> GetAccessibleTeamIdsAsync(ClaimsPrincipal user) => Task.FromResult<HashSet<Guid>?>(null);
    }

    private sealed class RestrictedTeamAccess : ITeamAccessService
    {
        private readonly HashSet<Guid> _allowed;
        public RestrictedTeamAccess(params Guid[] allowed) => _allowed = [.. allowed];
        public Task<HashSet<Guid>?> GetAccessibleTeamIdsAsync(ClaimsPrincipal user) => Task.FromResult<HashSet<Guid>?>(_allowed);
    }

    private sealed class NoopFileService : IFileService
    {
        public (bool IsValid, string? ErrorMessage) ValidateFile(IFormFile file, long maxSizeBytes, string[] allowedExtensions) => (true, null);
        public Task<string> SaveFileAsync(IFormFile file, string subfolder) => Task.FromResult("");
        public void DeleteFile(string relativePath) { }
        public string GetContentType(string filePath) => "application/octet-stream";
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

    private static Team MakeTeam(ApplicationDbContext db)
    {
        var team = new Team { Id = Guid.NewGuid(), Name = $"Team_{Guid.NewGuid():N}", CreatedAt = DateTime.UtcNow };
        db.Teams.Add(team);
        return team;
    }

    [Fact]
    public async Task SeasonService_DeleteSeasonAsync_ThrowsCleanError_WhenSeasonHasTrainings()
    {
        var db = await BuildDbAsync(nameof(SeasonService_DeleteSeasonAsync_ThrowsCleanError_WhenSeasonHasTrainings));
        var team = MakeTeam(db);
        var season = new Season { Id = Guid.NewGuid(), Name = "Test", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 30), IsActive = false, CreatedAt = DateTime.UtcNow };
        db.Seasons.Add(season);
        db.TrainingEvents.Add(new TrainingEvent
        {
            Id = Guid.NewGuid(), TeamId = team.Id, SeasonId = season.Id, Title = "T",
            StartTime = DateTime.UtcNow.AddDays(1), EndTime = DateTime.UtcNow.AddDays(1).AddHours(1), CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var svc = new SeasonService(db, new FakeCurrentLanguage());

        // Before the fix this hit SaveChangesAsync and threw a raw DbUpdateException (FK violation)
        // instead of a message a controller could turn into a 400.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DeleteSeasonAsync(season.Id));
        Assert.Contains("tréning", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TeamService_DeleteTeamAsync_ThrowsCleanError_WhenTeamHasTrainingHistory()
    {
        var db = await BuildDbAsync(nameof(TeamService_DeleteTeamAsync_ThrowsCleanError_WhenTeamHasTrainingHistory));
        var team = MakeTeam(db);
        db.TrainingEvents.Add(new TrainingEvent
        {
            Id = Guid.NewGuid(), TeamId = team.Id, SeasonId = Guid.NewGuid(), Title = "T",
            StartTime = DateTime.UtcNow.AddDays(-1), EndTime = DateTime.UtcNow.AddDays(-1).AddHours(1), CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var svc = new TeamService(db, new NoTeamAccess(), new NoopAudit(), new FakeCurrentLanguage());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DeleteTeamAsync(team.Id, AdminUser()));
        Assert.Contains("tréning", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UserService_DeleteUserAsync_Succeeds_WhenCoachOwnsTrainingSeries()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(opt => opt.UseInMemoryDatabase(nameof(UserService_DeleteUserAsync_Succeeds_WhenCoachOwnsTrainingSeries)));
        services.AddDataProtection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
        services.AddIdentityCore<ApplicationUser>(opt =>
        {
            opt.Password.RequiredLength = 4;
            opt.Password.RequireDigit = false;
            opt.Password.RequireUppercase = false;
            opt.Password.RequireLowercase = false;
            opt.Password.RequireNonAlphanumeric = false;
        }).AddEntityFrameworkStores<ApplicationDbContext>();

        var sp = services.BuildServiceProvider();
        var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var coach = new ApplicationUser { UserName = "coach_del", Email = "coach_del@test.sk", FirstName = "C", LastName = "Del", EmailConfirmed = true };
        await userManager.CreateAsync(coach, "pass");

        var team = MakeTeam(db);
        var season = new Season { Id = Guid.NewGuid(), Name = "S", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 30), IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Seasons.Add(season);
        db.TrainingSeries.Add(new TrainingSeries
        {
            Id = Guid.NewGuid(), TeamId = team.Id, CoachId = coach.Id, SeasonId = season.Id, Title = "Series",
            DaysOfWeek = 1, StartTime = new TimeOnly(17, 0), EndTime = new TimeOnly(18, 0), IsActive = true, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var svc = new UserService(db, userManager, new NoopAudit(), new NoopFileService(), new LoggerFactory().CreateLogger<UserService>(), new FakeCurrentLanguage());

        // Before the fix, TrainingSeries.CoachId (a Restrict FK) was never cleaned up here,
        // so this threw a raw DbUpdateException instead of deleting the user.
        await svc.DeleteUserAsync(coach.Id, AdminUser());

        Assert.Null(await userManager.FindByIdAsync(coach.Id));
        Assert.False(await db.TrainingSeries.AnyAsync(ts => ts.CoachId == coach.Id));
    }

    [Fact]
    public async Task UserService_DeleteUserAsync_Succeeds_WhenParentHasLinkedChildAndAbsenceRequests()
    {
        // Regression test for MeController.DeleteMyAccount previously calling _userManager.DeleteAsync
        // directly instead of UserService.DeleteUserAsync. ParentChild.ParentId and
        // AbsenceRequest.ChildId/ParentId are Restrict FKs, so self-deleting an ordinary Parent
        // account (the most common case, not just the Coach/TrainingSeries one above) used to throw
        // a raw DbUpdateException.
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(opt => opt.UseInMemoryDatabase(nameof(UserService_DeleteUserAsync_Succeeds_WhenParentHasLinkedChildAndAbsenceRequests)));
        services.AddDataProtection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
        services.AddIdentityCore<ApplicationUser>(opt =>
        {
            opt.Password.RequiredLength = 4;
            opt.Password.RequireDigit = false;
            opt.Password.RequireUppercase = false;
            opt.Password.RequireLowercase = false;
            opt.Password.RequireNonAlphanumeric = false;
        }).AddEntityFrameworkStores<ApplicationDbContext>();

        var sp = services.BuildServiceProvider();
        var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var parent = new ApplicationUser { UserName = "parent_del", Email = "parent_del@test.sk", FirstName = "P", LastName = "Del", EmailConfirmed = true };
        await userManager.CreateAsync(parent, "pass");
        var child = new ApplicationUser { UserName = "child_del", FirstName = "C", LastName = "Del", EmailConfirmed = true };
        await userManager.CreateAsync(child);

        db.ParentChildren.Add(new ParentChild { ParentId = parent.Id, ChildId = child.Id, IsPrimary = true });

        var team = MakeTeam(db);
        var season = new Season { Id = Guid.NewGuid(), Name = "S", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 30), IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Seasons.Add(season);
        var training = new TrainingEvent
        {
            Id = Guid.NewGuid(), TeamId = team.Id, SeasonId = season.Id, Title = "T",
            StartTime = DateTime.UtcNow.AddDays(-1), EndTime = DateTime.UtcNow.AddDays(-1).AddHours(1), CreatedAt = DateTime.UtcNow
        };
        db.TrainingEvents.Add(training);

        db.AttendanceRecords.Add(new AttendanceRecord
        {
            Id = Guid.NewGuid(), TrainingEventId = training.Id, ChildId = child.Id,
            Status = AttendanceStatus.Present, RecordedAt = DateTime.UtcNow
        });
        db.AbsenceRequests.Add(new AbsenceRequest
        {
            Id = Guid.NewGuid(), ChildId = child.Id, ParentId = parent.Id,
            DateFrom = DateTime.UtcNow, DateTo = DateTime.UtcNow.AddDays(1), CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var svc = new UserService(db, userManager, new NoopAudit(), new NoopFileService(), new LoggerFactory().CreateLogger<UserService>(), new FakeCurrentLanguage());

        await svc.DeleteUserAsync(parent.Id, AdminUser());

        Assert.Null(await userManager.FindByIdAsync(parent.Id));
        Assert.False(await db.ParentChildren.AnyAsync(pc => pc.ParentId == parent.Id));
        Assert.False(await db.AbsenceRequests.AnyAsync(a => a.ParentId == parent.Id));
    }

    [Fact]
    public async Task TrainingSeriesService_ThrowsUnauthorized_ForCoachOutsideTeam()
    {
        var db = await BuildDbAsync(nameof(TrainingSeriesService_ThrowsUnauthorized_ForCoachOutsideTeam));
        var team = MakeTeam(db);
        var otherTeamId = Guid.NewGuid();
        var season = new Season { Id = Guid.NewGuid(), Name = "S", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 30), IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Seasons.Add(season);
        var owningCoach = new ApplicationUser { Id = "owning-coach", UserName = "oc", Email = "oc@test.sk", FirstName = "Owning", LastName = "Coach" };
        db.Users.Add(owningCoach);
        var series = new TrainingSeries
        {
            Id = Guid.NewGuid(), TeamId = team.Id, CoachId = owningCoach.Id, SeasonId = season.Id, Title = "Series",
            DaysOfWeek = 1, StartTime = new TimeOnly(17, 0), EndTime = new TimeOnly(18, 0), IsActive = true, CreatedAt = DateTime.UtcNow
        };
        db.TrainingSeries.Add(series);
        await db.SaveChangesAsync();

        // A coach whose accessible teams do not include `team`. Reading or deleting a series
        // that belongs to someone else's team has to be refused at the service layer, not just
        // hidden in the UI.
        var svc = new TrainingSeriesService(db, new RestrictedTeamAccess(otherTeamId), new NoopAudit(), new FakeCurrentLanguage());
        var coachUser = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Coach")], "test"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.GetSeriesAsync(series.Id, coachUser));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.DeleteSeriesAsync(series.Id, coachUser));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.CreateSeriesAsync(new CreateTrainingSeriesRequest(team.Id, season.Id, "New", 1, new TimeOnly(17, 0), new TimeOnly(18, 0), null, "Other", null), owningCoach.Id, coachUser));
    }

    [Fact]
    public async Task TrainingSeriesService_GetSeriesAsync_List_FiltersToAccessibleTeams()
    {
        var db = await BuildDbAsync(nameof(TrainingSeriesService_GetSeriesAsync_List_FiltersToAccessibleTeams));
        var accessibleTeam = MakeTeam(db);
        var otherTeam = MakeTeam(db);
        var coach = new ApplicationUser { Id = "coach-1", UserName = "c", Email = "c@test.sk", FirstName = "C", LastName = "C" };
        db.Users.Add(coach);
        var season = new Season { Id = Guid.NewGuid(), Name = "S", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 30), IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Seasons.Add(season);
        db.TrainingSeries.AddRange(
            new TrainingSeries { Id = Guid.NewGuid(), TeamId = accessibleTeam.Id, CoachId = coach.Id, SeasonId = season.Id, Title = "Mine", DaysOfWeek = 1, StartTime = new TimeOnly(17, 0), EndTime = new TimeOnly(18, 0), IsActive = true, CreatedAt = DateTime.UtcNow },
            new TrainingSeries { Id = Guid.NewGuid(), TeamId = otherTeam.Id, CoachId = coach.Id, SeasonId = season.Id, Title = "NotMine", DaysOfWeek = 1, StartTime = new TimeOnly(17, 0), EndTime = new TimeOnly(18, 0), IsActive = true, CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var svc = new TrainingSeriesService(db, new RestrictedTeamAccess(accessibleTeam.Id), new NoopAudit(), new FakeCurrentLanguage());
        var coachUser = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Coach")], "test"));

        var result = await svc.GetSeriesAsync(coachUser);

        Assert.Single(result);
        Assert.Equal("Mine", result[0].Title);
    }
}
