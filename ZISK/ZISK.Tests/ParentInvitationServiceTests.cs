using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Services;

namespace ZISK.Tests;

public class ParentInvitationServiceTests
{
    private static async Task<(ApplicationDbContext db, UserManager<ApplicationUser> um, ParentInvitationService svc)> BuildAsync(string dbName)
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
        services.AddScoped<ParentInvitationService>();

        var sp = services.BuildServiceProvider();
        var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();

        return (db, scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(), scope.ServiceProvider.GetRequiredService<ParentInvitationService>());
    }

    private static ApplicationUser MakeUser(string name) => new()
    {
        UserName = name,
        Email = $"{name}@test.sk",
        FirstName = "Test",
        LastName = name,
        EmailConfirmed = true
    };

    private static async Task LinkParentChild(ApplicationDbContext db, string parentId, string childId)
    {
        db.ParentChildren.Add(new ParentChild { ParentId = parentId, ChildId = childId, IsPrimary = true });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task IssueManualCode_GeneratesEightCharCode()
    {
        var (db, um, svc) = await BuildAsync(nameof(IssueManualCode_GeneratesEightCharCode));
        var parent = MakeUser("parent1");
        var child = MakeUser("child1");
        await um.CreateAsync(parent, "pass");
        await um.CreateAsync(child, "pass");
        await LinkParentChild(db, parent.Id, child.Id);

        var result = await svc.IssueManualCodeAsync(child.Id, parent.Id);

        Assert.Equal(InvitationIssueStatus.Success, result.Status);
        Assert.NotNull(result.PlainCode);
        // Display code is XXXX-XXXX = 9 chars with dash
        Assert.Equal(9, result.PlainCode!.Length);
        Assert.Equal('-', result.PlainCode[4]);
    }

    [Fact]
    public async Task IssueManualCode_Forbidden_WhenNotParent()
    {
        var (db, um, svc) = await BuildAsync(nameof(IssueManualCode_Forbidden_WhenNotParent));
        var stranger = MakeUser("stranger");
        var child = MakeUser("child2");
        await um.CreateAsync(stranger, "pass");
        await um.CreateAsync(child, "pass");

        var result = await svc.IssueManualCodeAsync(child.Id, stranger.Id);

        Assert.Equal(InvitationIssueStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task RedeemManualCode_Success()
    {
        var (db, um, svc) = await BuildAsync(nameof(RedeemManualCode_Success));
        var parent1 = MakeUser("p1");
        var parent2 = MakeUser("p2");
        var child = MakeUser("c1");
        await um.CreateAsync(parent1, "pass");
        await um.CreateAsync(parent2, "pass");
        await um.CreateAsync(child, "pass");
        await LinkParentChild(db, parent1.Id, child.Id);

        var issue = await svc.IssueManualCodeAsync(child.Id, parent1.Id);
        var redeem = await svc.RedeemManualCodeAsync(issue.PlainCode!, parent2.Id);

        Assert.Equal(InvitationRedeemStatus.Success, redeem.Status);
        var link = await db.ParentChildren.AnyAsync(pc => pc.ParentId == parent2.Id && pc.ChildId == child.Id);
        Assert.True(link);
    }

    [Fact]
    public async Task RedeemManualCode_TooManyAttempts_AfterFiveWrong()
    {
        var (db, um, svc) = await BuildAsync(nameof(RedeemManualCode_TooManyAttempts_AfterFiveWrong));
        var parent = MakeUser("p3");
        var child = MakeUser("c2");
        var redeemer = MakeUser("r1");
        await um.CreateAsync(parent, "pass");
        await um.CreateAsync(child, "pass");
        await um.CreateAsync(redeemer, "pass");
        await LinkParentChild(db, parent.Id, child.Id);

        var issue = await svc.IssueManualCodeAsync(child.Id, parent.Id);

        // Simulate exhausted attempts by setting AttemptCount directly
        var inv = await db.ParentInvitations.FirstAsync();
        inv.AttemptCount = ParentInvitation.MaxAttempts;
        await db.SaveChangesAsync();

        var result = await svc.RedeemManualCodeAsync(issue.PlainCode!, redeemer.Id);
        Assert.Equal(InvitationRedeemStatus.TooManyAttempts, result.Status);
    }

    [Fact]
    public async Task RedeemEmailLink_Expired_AfterExpiry()
    {
        var (db, um, svc) = await BuildAsync(nameof(RedeemEmailLink_Expired_AfterExpiry));
        var parent1 = MakeUser("ep1");
        var parent2 = MakeUser("ep2");
        var child = MakeUser("ec1");
        await um.CreateAsync(parent1, "pass");
        await um.CreateAsync(parent2, "pass");
        await um.CreateAsync(child, "pass");
        await LinkParentChild(db, parent1.Id, child.Id);

        var issue = await svc.IssueEmailLinkAsync(child.Id, parent1.Id, "ep2@test.sk");
        var inv = await db.ParentInvitations.FirstAsync();
        inv.ExpiresAt = DateTime.UtcNow.AddHours(-1);
        await db.SaveChangesAsync();

        var result = await svc.RedeemEmailLinkAsync(issue.PlainCode!, parent2.Id);
        Assert.Equal(InvitationRedeemStatus.Expired, result.Status);
    }

    [Fact]
    public async Task RedeemEmailLink_AlreadyUsed_SecondRedeem()
    {
        var (db, um, svc) = await BuildAsync(nameof(RedeemEmailLink_AlreadyUsed_SecondRedeem));
        var parent1 = MakeUser("au1");
        var parent2 = MakeUser("au2");
        var parent3 = MakeUser("au3");
        var child = MakeUser("auc1");
        await um.CreateAsync(parent1, "pass");
        await um.CreateAsync(parent2, "pass");
        await um.CreateAsync(parent3, "pass");
        await um.CreateAsync(child, "pass");
        await LinkParentChild(db, parent1.Id, child.Id);

        var issue = await svc.IssueEmailLinkAsync(child.Id, parent1.Id, "au2@test.sk");
        await svc.RedeemEmailLinkAsync(issue.PlainCode!, parent2.Id);
        var secondResult = await svc.RedeemEmailLinkAsync(issue.PlainCode!, parent3.Id);

        Assert.Equal(InvitationRedeemStatus.AlreadyUsed, secondResult.Status);
    }

    [Fact]
    public async Task IssueManualCode_TooManyActive_AfterThree()
    {
        var (db, um, svc) = await BuildAsync(nameof(IssueManualCode_TooManyActive_AfterThree));
        var parent = MakeUser("lp1");
        var child = MakeUser("lc1");
        await um.CreateAsync(parent, "pass");
        await um.CreateAsync(child, "pass");
        await LinkParentChild(db, parent.Id, child.Id);

        for (int i = 0; i < ParentInvitation.MaxActiveInvitations; i++)
            await svc.IssueManualCodeAsync(child.Id, parent.Id);

        var result = await svc.IssueManualCodeAsync(child.Id, parent.Id);
        Assert.Equal(InvitationIssueStatus.TooManyActive, result.Status);
    }
}
