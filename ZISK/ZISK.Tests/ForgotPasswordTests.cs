using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZISK.Data;

namespace ZISK.Tests;

public class ForgotPasswordTests
{
    private static async Task<(UserManager<ApplicationUser> userManager, IServiceProvider sp)> BuildAsync(string dbName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        services.AddDataProtection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.None));
        services.AddIdentityCore<ApplicationUser>(opt =>
        {
            opt.Password.RequiredLength = 8;
            opt.Password.RequireDigit = true;
            opt.Password.RequireUppercase = true;
            opt.Password.RequireLowercase = true;
            opt.Password.RequireNonAlphanumeric = false;
            opt.SignIn.RequireConfirmedAccount = true;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        var sp = services.BuildServiceProvider();

        // apply schema
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();

        return (sp.GetRequiredService<UserManager<ApplicationUser>>(), sp);
    }

    private static ApplicationUser MakeUser(string email, string? username = null) => new()
    {
        UserName = username ?? email,
        Email = email,
        FirstName = "Test",
        LastName = "User",
        EmailConfirmed = true
    };

    [Fact]
    public async Task FindByEmail_ReturnsUser_WhenEmailExists()
    {
        var (um, _) = await BuildAsync(nameof(FindByEmail_ReturnsUser_WhenEmailExists));
        var user = MakeUser("test@example.com");
        await um.CreateAsync(user, "Password1");

        var found = await um.FindByEmailAsync("test@example.com")
                 ?? await um.FindByNameAsync("test@example.com");

        Assert.NotNull(found);
        Assert.Equal("test@example.com", found.Email);
    }

    [Fact]
    public async Task FindByUsername_ReturnsUser_WhenUsernameExists()
    {
        var (um, _) = await BuildAsync(nameof(FindByUsername_ReturnsUser_WhenUsernameExists));
        var user = MakeUser("user2@example.com", "jan.novak");
        await um.CreateAsync(user, "Password1");

        var found = await um.FindByEmailAsync("jan.novak")
                 ?? await um.FindByNameAsync("jan.novak");

        Assert.NotNull(found);
        Assert.Equal("jan.novak", found.UserName);
    }

    [Fact]
    public async Task LookupReturnsNull_ForNonExistentUser()
    {
        var (um, _) = await BuildAsync(nameof(LookupReturnsNull_ForNonExistentUser));

        var found = await um.FindByEmailAsync("nobody@example.com")
                 ?? await um.FindByNameAsync("nobody@example.com");

        Assert.Null(found);
    }

    [Fact]
    public async Task UnconfirmedUser_TreatedSameAsNotFound_AntiEnumeration()
    {
        var (um, _) = await BuildAsync(nameof(UnconfirmedUser_TreatedSameAsNotFound_AntiEnumeration));
        var user = MakeUser("unconfirmed@example.com");
        user.EmailConfirmed = false;
        await um.CreateAsync(user, "Password1");

        var found = await um.FindByEmailAsync("unconfirmed@example.com")
                 ?? await um.FindByNameAsync("unconfirmed@example.com");

        // User exists but email not confirmed → should redirect without sending email
        var isConfirmed = found is not null && await um.IsEmailConfirmedAsync(found);
        Assert.False(isConfirmed);
    }

    [Fact]
    public async Task ConfirmedUser_IsEligible_ForPasswordReset()
    {
        var (um, _) = await BuildAsync(nameof(ConfirmedUser_IsEligible_ForPasswordReset));
        var user = MakeUser("confirmed@example.com");
        await um.CreateAsync(user, "Password1");

        var found = await um.FindByEmailAsync("confirmed@example.com");
        Assert.NotNull(found);
        Assert.True(await um.IsEmailConfirmedAsync(found));

        // verify reset token can be generated
        var token = await um.GeneratePasswordResetTokenAsync(found);
        Assert.NotEmpty(token);
    }

    [Fact]
    public async Task ResetPassword_Succeeds_WithValidToken()
    {
        var (um, _) = await BuildAsync(nameof(ResetPassword_Succeeds_WithValidToken));
        var user = MakeUser("reset@example.com");
        await um.CreateAsync(user, "OldPass1");

        var token = await um.GeneratePasswordResetTokenAsync(user);
        var result = await um.ResetPasswordAsync(user, token, "NewPass1");

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task ResetPassword_Fails_WithInvalidToken()
    {
        var (um, _) = await BuildAsync(nameof(ResetPassword_Fails_WithInvalidToken));
        var user = MakeUser("reset2@example.com");
        await um.CreateAsync(user, "OldPass1");

        var result = await um.ResetPasswordAsync(user, "invalid-token", "NewPass1");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task ResetPassword_Fails_WithWeakPassword()
    {
        var (um, _) = await BuildAsync(nameof(ResetPassword_Fails_WithWeakPassword));
        var user = MakeUser("weak@example.com");
        await um.CreateAsync(user, "ValidPass1");

        var token = await um.GeneratePasswordResetTokenAsync(user);
        var result = await um.ResetPasswordAsync(user, token, "weak");

        Assert.False(result.Succeeded);
    }
}
