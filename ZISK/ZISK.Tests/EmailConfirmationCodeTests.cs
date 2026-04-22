using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Services;

namespace ZISK.Tests;

public class EmailConfirmationCodeTests
{
    private static async Task<(ApplicationDbContext db, UserManager<ApplicationUser> um, EmailConfirmationCodeService svc)> BuildAsync(string dbName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        services.AddDataProtection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.None));
        services.AddIdentityCore<ApplicationUser>(opt =>
        {
            opt.Password.RequiredLength = 8;
            opt.Password.RequireDigit = true;
            opt.Password.RequireUppercase = true;
            opt.Password.RequireLowercase = true;
            opt.Password.RequireNonAlphanumeric = false;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();
        services.AddScoped<EmailConfirmationCodeService>();

        var sp = services.BuildServiceProvider();
        var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();

        return (
            db,
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            scope.ServiceProvider.GetRequiredService<EmailConfirmationCodeService>());
    }

    private static ApplicationUser MakeUser(string email) => new()
    {
        UserName = email,
        Email = email,
        FirstName = "Test",
        LastName = "User",
        EmailConfirmed = false
    };

    [Fact]
    public async Task IssueAsync_ReturnsSixDigitCode()
    {
        var (_, um, svc) = await BuildAsync(nameof(IssueAsync_ReturnsSixDigitCode));
        var user = MakeUser("a@example.com");
        await um.CreateAsync(user, "Password1");

        var code = await svc.IssueAsync(user);

        Assert.Equal(6, code.Length);
        Assert.All(code, c => Assert.True(char.IsDigit(c)));
    }

    [Fact]
    public async Task IssueAsync_StoresHashNotPlaintext()
    {
        var (db, um, svc) = await BuildAsync(nameof(IssueAsync_StoresHashNotPlaintext));
        var user = MakeUser("b@example.com");
        await um.CreateAsync(user, "Password1");

        var code = await svc.IssueAsync(user);
        var stored = await db.EmailConfirmationCodes.SingleAsync(e => e.UserId == user.Id);

        Assert.NotEqual(code, stored.CodeHash);
        Assert.Equal(EmailConfirmationCodeService.HashCode(code), stored.CodeHash);
    }

    [Fact]
    public async Task IssueAsync_InvalidatesPreviousUnusedCodes()
    {
        var (db, um, svc) = await BuildAsync(nameof(IssueAsync_InvalidatesPreviousUnusedCodes));
        var user = MakeUser("c@example.com");
        await um.CreateAsync(user, "Password1");

        await svc.IssueAsync(user);
        await svc.IssueAsync(user);

        var count = await db.EmailConfirmationCodes.CountAsync(e => e.UserId == user.Id && e.UsedAt == null);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task RedeemAsync_ReturnsSuccess_ForValidCode()
    {
        var (_, um, svc) = await BuildAsync(nameof(RedeemAsync_ReturnsSuccess_ForValidCode));
        var user = MakeUser("d@example.com");
        await um.CreateAsync(user, "Password1");

        var code = await svc.IssueAsync(user);
        var result = await svc.RedeemAsync(user.Id, code);

        Assert.Equal(CodeRedemptionResult.Success, result);
    }

    [Fact]
    public async Task RedeemAsync_MarksCodeUsed_OnSuccess()
    {
        var (db, um, svc) = await BuildAsync(nameof(RedeemAsync_MarksCodeUsed_OnSuccess));
        var user = MakeUser("e@example.com");
        await um.CreateAsync(user, "Password1");

        var code = await svc.IssueAsync(user);
        await svc.RedeemAsync(user.Id, code);

        var entry = await db.EmailConfirmationCodes.SingleAsync(e => e.UserId == user.Id);
        Assert.NotNull(entry.UsedAt);
    }

    [Fact]
    public async Task RedeemAsync_ReturnsInvalid_ForWrongCode()
    {
        var (_, um, svc) = await BuildAsync(nameof(RedeemAsync_ReturnsInvalid_ForWrongCode));
        var user = MakeUser("f@example.com");
        await um.CreateAsync(user, "Password1");

        await svc.IssueAsync(user);
        var result = await svc.RedeemAsync(user.Id, "000000");

        Assert.Equal(CodeRedemptionResult.Invalid, result);
    }

    [Fact]
    public async Task RedeemAsync_ReturnsExpired_WhenPastExpiry()
    {
        var (db, um, svc) = await BuildAsync(nameof(RedeemAsync_ReturnsExpired_WhenPastExpiry));
        var user = MakeUser("g@example.com");
        await um.CreateAsync(user, "Password1");

        var code = await svc.IssueAsync(user);
        var entry = await db.EmailConfirmationCodes.SingleAsync(e => e.UserId == user.Id);
        entry.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var result = await svc.RedeemAsync(user.Id, code);

        Assert.Equal(CodeRedemptionResult.Expired, result);
    }

    [Fact]
    public async Task RedeemAsync_ReturnsTooManyAttempts_OnSixthAttempt()
    {
        var (_, um, svc) = await BuildAsync(nameof(RedeemAsync_ReturnsTooManyAttempts_OnSixthAttempt));
        var user = MakeUser("h@example.com");
        await um.CreateAsync(user, "Password1");

        var code = await svc.IssueAsync(user);

        for (int i = 0; i < EmailConfirmationCode.MaxAttempts; i++)
            await svc.RedeemAsync(user.Id, "999999");

        var resultAfterLockout = await svc.RedeemAsync(user.Id, code);

        Assert.Equal(CodeRedemptionResult.TooManyAttempts, resultAfterLockout);
    }

    [Fact]
    public async Task RedeemAsync_ReturnsNotFound_WhenUserHasNoCode()
    {
        var (_, um, svc) = await BuildAsync(nameof(RedeemAsync_ReturnsNotFound_WhenUserHasNoCode));
        var user = MakeUser("i@example.com");
        await um.CreateAsync(user, "Password1");

        var result = await svc.RedeemAsync(user.Id, "123456");

        Assert.Equal(CodeRedemptionResult.NotFound, result);
    }

    [Fact]
    public async Task RedeemAsync_WrongCodeLastAttempt_ReturnsTooManyAttempts()
    {
        var (_, um, svc) = await BuildAsync(nameof(RedeemAsync_WrongCodeLastAttempt_ReturnsTooManyAttempts));
        var user = MakeUser("j@example.com");
        await um.CreateAsync(user, "Password1");

        await svc.IssueAsync(user);

        for (int i = 0; i < EmailConfirmationCode.MaxAttempts - 1; i++)
            await svc.RedeemAsync(user.Id, "000000");

        var final = await svc.RedeemAsync(user.Id, "000000");

        Assert.Equal(CodeRedemptionResult.TooManyAttempts, final);
    }

    [Fact]
    public async Task HashCode_IsDeterministic_ForSameInput()
    {
        var hash1 = EmailConfirmationCodeService.HashCode("123456");
        var hash2 = EmailConfirmationCodeService.HashCode("123456");
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public async Task HashCode_Differs_ForDifferentCodes()
    {
        var hash1 = EmailConfirmationCodeService.HashCode("123456");
        var hash2 = EmailConfirmationCodeService.HashCode("123457");
        Assert.NotEqual(hash1, hash2);
        await Task.CompletedTask;
    }
}
