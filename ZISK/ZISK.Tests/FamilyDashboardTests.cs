using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using ZISK.Controllers;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Services;
using ZISK.Shared.DTOs.Children;

namespace ZISK.Tests;

public class FamilyDashboardTests
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
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
            => Task.CompletedTask;
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

    private static ApplicationUser MakeUser(string suffix) => new()
    {
        Id = Guid.NewGuid().ToString(),
        UserName = $"u_{suffix}",
        Email = $"u_{suffix}@test.sk",
        FirstName = "Test",
        LastName = suffix,
        EmailConfirmed = true,
        IsActive = true,
        DateOfBirth = DateOnly.FromDateTime(DateTime.Today.AddYears(-10))
    };

    private static Team MakeTeam(ApplicationDbContext db, string name = "TestTeam")
    {
        var team = new Team { Id = Guid.NewGuid(), Name = name, CreatedAt = DateTime.UtcNow };
        db.Teams.Add(team);
        return team;
    }

    private static TeamMember AddMember(ApplicationDbContext db, Team team, string userId)
    {
        var tm = new TeamMember { TeamId = team.Id, UserId = userId, JoinedAt = DateTime.UtcNow };
        db.TeamMembers.Add(tm);
        return tm;
    }

    private static ClaimsPrincipal MakePrincipal(string userId, string role)
        => new(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, role)
        ], "test"));

    private static ChildrenController MakeController(
        ApplicationDbContext db,
        ClaimsPrincipal user,
        ITeamAccessService? teamAccess = null)
    {
        var loggerFactory = new LoggerFactory();
        var emailSender = new SmtpEmailSender(
            Microsoft.Extensions.Options.Options.Create(new SmtpSettings()),
            loggerFactory.CreateLogger<SmtpEmailSender>());

        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddIdentityCore<ApplicationUser>()
            .AddEntityFrameworkStores<ApplicationDbContext>();
        var sp = services.BuildServiceProvider();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();

        var controller = new ChildrenController(
            db,
            userManager,
            new UsernameGenerator(userManager),
            new NoopAudit(),
            emailSender,
            teamAccess ?? new NoTeamAccess(),
            loggerFactory.CreateLogger<ChildrenController>());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
        return controller;
    }

    private static AttendanceService MakeAttendanceSvc(ApplicationDbContext db,
        ITeamAccessService? teamAccess = null)
        => new(db, teamAccess ?? new NoTeamAccess(), new NoopAudit());

    private static ExcuseService MakeExcuseSvc(ApplicationDbContext db,
        ITeamAccessService? teamAccess = null)
        => new(db, teamAccess ?? new NoTeamAccess(), new NoopAudit());

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMyChildren_ParentWithTwoChildren_ReturnsBoth()
    {
        var db = await BuildDbAsync(nameof(GetMyChildren_ParentWithTwoChildren_ReturnsBoth));
        var parent = MakeUser("parent");
        var child1 = MakeUser("child1");
        var child2 = MakeUser("child2");
        db.Users.AddRange(parent, child1, child2);

        db.ParentChildren.AddRange(
            new ParentChild { ParentId = parent.Id, ChildId = child1.Id, IsPrimary = true },
            new ParentChild { ParentId = parent.Id, ChildId = child2.Id, IsPrimary = false });

        await db.SaveChangesAsync();

        var ctrl = MakeController(db, MakePrincipal(parent.Id, "Parent"));
        var result = await ctrl.GetMyChildren();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsType<List<ChildDto>>(ok.Value);
        Assert.Equal(2, list.Count);
        Assert.Contains(list, c => c.Id == child1.Id);
        Assert.Contains(list, c => c.Id == child2.Id);
    }

    [Fact]
    public async Task GetMyAttendance_ParentWithTwoChildren_ReturnsBothChildrenRecords()
    {
        var db = await BuildDbAsync(nameof(GetMyAttendance_ParentWithTwoChildren_ReturnsBothChildrenRecords));
        var parent = MakeUser("parent");
        var child1 = MakeUser("child1");
        var child2 = MakeUser("child2");
        var team = MakeTeam(db);
        db.Users.AddRange(parent, child1, child2);

        db.ParentChildren.AddRange(
            new ParentChild { ParentId = parent.Id, ChildId = child1.Id, IsPrimary = true },
            new ParentChild { ParentId = parent.Id, ChildId = child2.Id, IsPrimary = false });

        var season = new Data.Entities.Season
        {
            Id = Guid.NewGuid(),
            Name = "2024/25",
            StartDate = new DateOnly(2024, 8, 1),
            EndDate = new DateOnly(2025, 7, 31)
        };
        db.Seasons.Add(season);

        var training = new TrainingEvent
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            SeasonId = season.Id,
            Title = "Tréning",
            StartTime = DateTime.UtcNow.AddDays(-5),
            EndTime = DateTime.UtcNow.AddDays(-5).AddHours(1.5),
            CreatedAt = DateTime.UtcNow
        };
        db.TrainingEvents.Add(training);

        db.AttendanceRecords.AddRange(
            new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                TrainingEventId = training.Id,
                ChildId = child1.Id,
                Status = Data.Entities.AttendanceStatus.Present,
                RecordedAt = DateTime.UtcNow
            },
            new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                TrainingEventId = training.Id,
                ChildId = child2.Id,
                Status = Data.Entities.AttendanceStatus.Absent,
                RecordedAt = DateTime.UtcNow
            });

        await db.SaveChangesAsync();

        var svc = MakeAttendanceSvc(db);
        var parentUser = MakePrincipal(parent.Id, "Parent");
        var result = await svc.GetMyAttendanceAsync(parentUser, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.ChildId == child1.Id);
        Assert.Contains(result, r => r.ChildId == child2.Id);
    }

    [Fact]
    public async Task CreateExcuse_ChildRole_Throws()
    {
        var db = await BuildDbAsync(nameof(CreateExcuse_ChildRole_Throws));
        var child = MakeUser("child");
        db.Users.Add(child);
        await db.SaveChangesAsync();

        var svc = MakeExcuseSvc(db);
        var childUser = MakePrincipal(child.Id, "Child");

        var req = new ZISK.Shared.DTOs.Excuses.CreateExcuseRequest(
            child.Id, null, DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-1), null, null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.CreateExcuseAsync(req, childUser));
    }

    [Fact]
    public async Task UpdateChild_ParentChangesName_Succeeds()
    {
        var db = await BuildDbAsync(nameof(UpdateChild_ParentChangesName_Succeeds));
        var parent = MakeUser("parent");
        var child = MakeUser("child");
        db.Users.AddRange(parent, child);
        db.ParentChildren.Add(new ParentChild { ParentId = parent.Id, ChildId = child.Id, IsPrimary = true });
        await db.SaveChangesAsync();

        var ctrl = MakeController(db, MakePrincipal(parent.Id, "Parent"));
        var req = new UpdateChildRequest("Nové", "Meno", null, new DateOnly(2015, 6, 15), null);
        var result = await ctrl.UpdateChild(child.Id, req);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ChildDetailDto>(ok.Value);
        Assert.Equal("Nové", dto.FirstName);
        Assert.Equal("Meno", dto.LastName);
    }

    [Fact]
    public async Task UpdateChild_ParentAttemptsTeamChange_ReturnsBadRequest()
    {
        var db = await BuildDbAsync(nameof(UpdateChild_ParentAttemptsTeamChange_ReturnsBadRequest));
        var parent = MakeUser("parent");
        var child = MakeUser("child");
        var team = MakeTeam(db, "Tím A");
        db.Users.AddRange(parent, child);
        db.ParentChildren.Add(new ParentChild { ParentId = parent.Id, ChildId = child.Id, IsPrimary = true });
        await db.SaveChangesAsync();

        var ctrl = MakeController(db, MakePrincipal(parent.Id, "Parent"));
        var req = new UpdateChildRequest("Meno", "Priezvisko", null, new DateOnly(2015, 6, 15), team.Id);
        var result = await ctrl.UpdateChild(child.Id, req);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateChild_ParentAccessesOtherChild_ReturnsForbid()
    {
        var db = await BuildDbAsync(nameof(UpdateChild_ParentAccessesOtherChild_ReturnsForbid));
        var parent = MakeUser("parent");
        var otherChild = MakeUser("otherchild");
        db.Users.AddRange(parent, otherChild);
        await db.SaveChangesAsync();

        var ctrl = MakeController(db, MakePrincipal(parent.Id, "Parent"));
        var req = new UpdateChildRequest("Meno", "Priezvisko", null, new DateOnly(2015, 1, 1), null);
        var result = await ctrl.UpdateChild(otherChild.Id, req);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task UpdateChild_AdminChangesTeam_Succeeds()
    {
        var db = await BuildDbAsync(nameof(UpdateChild_AdminChangesTeam_Succeeds));
        var admin = MakeUser("admin");
        var child = MakeUser("child");
        var teamA = MakeTeam(db, "Tím A");
        var teamB = MakeTeam(db, "Tím B");
        db.Users.AddRange(admin, child);
        AddMember(db, teamA, child.Id);
        await db.SaveChangesAsync();

        var ctrl = MakeController(db, MakePrincipal(admin.Id, "Admin"));
        var req = new UpdateChildRequest("Meno", "Priezvisko", null, new DateOnly(2014, 3, 10), teamB.Id);
        var result = await ctrl.UpdateChild(child.Id, req);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ChildDetailDto>(ok.Value);
        Assert.Equal(teamB.Id, dto.TeamId);
        Assert.Equal("Tím B", dto.TeamName);
    }
}
