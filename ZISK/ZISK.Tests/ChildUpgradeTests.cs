using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZISK.Data;
using ZISK.Services;

namespace ZISK.Tests;

public class ChildUpgradeTests
{
    private sealed class NoopEmailSender : SmtpEmailSender
    {
        public NoopEmailSender() : base(
            Options.Create(new SmtpSettings { Host = "localhost", Port = 25, SenderEmail = "test@test.sk", SenderName = "Test" }),
            new LoggerFactory().CreateLogger<SmtpEmailSender>())
        { }

        public override Task SendEmailAsync(string email, string subject, string htmlMessage) => Task.CompletedTask;
    }

    private sealed class NoopAudit : IAuditService
    {
        public void Log(string action, string entityType, string entityId, System.Security.Claims.ClaimsPrincipal? user, object? details) { }
    }

    private static async Task<(IServiceScope scope, ApplicationDbContext db, UserManager<ApplicationUser> um)> BuildAsync(string dbName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        services.AddDataProtection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
        services.AddIdentityCore<ApplicationUser>(opt =>
        {
            opt.Password.RequiredLength = 4;
            opt.Password.RequireDigit = false;
            opt.Password.RequireUppercase = false;
            opt.Password.RequireLowercase = false;
            opt.Password.RequireNonAlphanumeric = false;
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        var sp = services.BuildServiceProvider();
        var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync("Child"))
            await roleManager.CreateAsync(new IdentityRole("Child"));
        if (!await roleManager.RoleExistsAsync("Athlete"))
            await roleManager.CreateAsync(new IdentityRole("Athlete"));

        return (scope, db, scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>());
    }

    private static ChildUpgradeWorker MakeWorker(ApplicationDbContext db, UserManager<ApplicationUser> um)
        => new(db, um, new NoopEmailSender(), new NoopAudit(), new LoggerFactory().CreateLogger<ChildUpgradeWorker>());

    private static ApplicationUser MakeChild(DateOnly dob) => new()
    {
        UserName = $"child_{Guid.NewGuid():N}",
        Email = $"child_{Guid.NewGuid():N}@test.sk",
        FirstName = "Test",
        LastName = "Child",
        DateOfBirth = dob,
        EmailConfirmed = true
    };

    [Fact]
    public async Task RunPass_UpgradesChild_WhenTurns18()
    {
        var (_, db, um) = await BuildAsync(nameof(RunPass_UpgradesChild_WhenTurns18));

        var child = MakeChild(DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-18).AddDays(-1));
        await um.CreateAsync(child, "pass");
        await um.AddToRoleAsync(child, "Child");

        await MakeWorker(db, um).RunPassAsync();

        var roles = await um.GetRolesAsync(child);
        Assert.Contains("Athlete", roles);
        Assert.DoesNotContain("Child", roles);
    }

    [Fact]
    public async Task RunPass_DoesNotUpgrade_WhenUnder18()
    {
        var (_, db, um) = await BuildAsync(nameof(RunPass_DoesNotUpgrade_WhenUnder18));

        var child = MakeChild(DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-17));
        await um.CreateAsync(child, "pass");
        await um.AddToRoleAsync(child, "Child");

        await MakeWorker(db, um).RunPassAsync();

        var roles = await um.GetRolesAsync(child);
        Assert.Contains("Child", roles);
        Assert.DoesNotContain("Athlete", roles);
    }

    [Fact]
    public async Task RunPass_PreservesParentChildLinks()
    {
        var (_, db, um) = await BuildAsync(nameof(RunPass_PreservesParentChildLinks));

        var parent = new ApplicationUser
        {
            UserName = "parent_upgrade", Email = "parent@test.sk", FirstName = "Parent", LastName = "Test",
            EmailConfirmed = true
        };
        await um.CreateAsync(parent, "pass");

        var child = MakeChild(DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-18).AddDays(-1));
        await um.CreateAsync(child, "pass");
        await um.AddToRoleAsync(child, "Child");
        db.ParentChildren.Add(new Data.ParentChild { ParentId = parent.Id, ChildId = child.Id, IsPrimary = true });
        await db.SaveChangesAsync();

        await MakeWorker(db, um).RunPassAsync();

        var linkStillExists = await db.ParentChildren.AnyAsync(pc => pc.ChildId == child.Id && pc.ParentId == parent.Id);
        Assert.True(linkStillExists);
    }
}
