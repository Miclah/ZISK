using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZISK.Data;

namespace ZISK.Tests;

public class ChangeEmailTests
{
    private static async Task<UserManager<ApplicationUser>> BuildAsync(string dbName)
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

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreatedAsync();
        return sp.GetRequiredService<UserManager<ApplicationUser>>();
    }

    private static ApplicationUser MakeUser(string email, string? username = null) => new()
    {
        UserName = username ?? email,
        Email = email,
        FirstName = "Test",
        LastName = "User",
        EmailConfirmed = true
    };

    // ─── Email availability ──────────────────────────────────────────────────

    [Fact]
    public async Task EmailAvailability_ReturnsTaken_WhenOtherUserHasEmail()
    {
        var um = await BuildAsync(nameof(EmailAvailability_ReturnsTaken_WhenOtherUserHasEmail));
        var userA = MakeUser("a@example.com");
        var userB = MakeUser("b@example.com");
        await um.CreateAsync(userA, "Password1");
        await um.CreateAsync(userB, "Password1");

        // userB tries to change to a@example.com
        var existing = await um.FindByEmailAsync("a@example.com");
        var isTaken = existing is not null && existing.Id != userB.Id;

        Assert.True(isTaken);
    }

    [Fact]
    public async Task EmailAvailability_ReturnsAvailable_ForFreeEmail()
    {
        var um = await BuildAsync(nameof(EmailAvailability_ReturnsAvailable_ForFreeEmail));
        var user = MakeUser("user@example.com");
        await um.CreateAsync(user, "Password1");

        var existing = await um.FindByEmailAsync("new@example.com");
        var isTaken = existing is not null && existing.Id != user.Id;

        Assert.False(isTaken);
    }

    [Fact]
    public async Task EmailAvailability_SameUserSameEmail_NotTaken()
    {
        var um = await BuildAsync(nameof(EmailAvailability_SameUserSameEmail_NotTaken));
        var user = MakeUser("same@example.com");
        await um.CreateAsync(user, "Password1");

        var existing = await um.FindByEmailAsync("same@example.com");
        var isTaken = existing is not null && existing.Id != user.Id;

        Assert.False(isTaken);
    }

    // ─── Username preservation on email change ───────────────────────────────

    [Fact]
    public async Task ConfirmEmailChange_UpdatesUsername_WhenUsernameMatchedOldEmail()
    {
        var um = await BuildAsync(nameof(ConfirmEmailChange_UpdatesUsername_WhenUsernameMatchedOldEmail));
        var user = MakeUser("old@example.com");
        await um.CreateAsync(user, "Password1");

        // Verify: UserName == Email before change
        Assert.Equal(user.Email, user.UserName);

        var originalEmailMatchesUsername = string.Equals(user.UserName, user.Email, StringComparison.OrdinalIgnoreCase);

        var token = await um.GenerateChangeEmailTokenAsync(user, "new@example.com");
        await um.ChangeEmailAsync(user, "new@example.com", token);

        if (originalEmailMatchesUsername)
            await um.SetUserNameAsync(user, "new@example.com");

        var updated = await um.FindByIdAsync(user.Id);
        Assert.Equal("new@example.com", updated!.UserName);
    }

    [Fact]
    public async Task ConfirmEmailChange_PreservesUsername_WhenUsernameWasCustom()
    {
        var um = await BuildAsync(nameof(ConfirmEmailChange_PreservesUsername_WhenUsernameWasCustom));
        var user = MakeUser("old2@example.com", username: "jan.novak");
        await um.CreateAsync(user, "Password1");

        // Verify: UserName != Email before change
        Assert.NotEqual(user.Email, user.UserName);

        var originalEmailMatchesUsername = string.Equals(user.UserName, user.Email, StringComparison.OrdinalIgnoreCase);

        var token = await um.GenerateChangeEmailTokenAsync(user, "new2@example.com");
        await um.ChangeEmailAsync(user, "new2@example.com", token);

        if (originalEmailMatchesUsername)
            await um.SetUserNameAsync(user, "new2@example.com");

        var updated = await um.FindByIdAsync(user.Id);
        Assert.Equal("jan.novak", updated!.UserName);    // username preserved
        Assert.Equal("new2@example.com", updated.Email); // email changed
    }

    // ─── ChangePassword ──────────────────────────────────────────────────────

    [Fact]
    public async Task ChangePassword_Fails_WithWrongOldPassword()
    {
        var um = await BuildAsync(nameof(ChangePassword_Fails_WithWrongOldPassword));
        var user = MakeUser("cp@example.com");
        await um.CreateAsync(user, "OldPass1");

        var result = await um.ChangePasswordAsync(user, "WrongOld1", "NewPass1");
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task ChangePassword_Succeeds_WithCorrectOldPassword()
    {
        var um = await BuildAsync(nameof(ChangePassword_Succeeds_WithCorrectOldPassword));
        var user = MakeUser("cp2@example.com");
        await um.CreateAsync(user, "OldPass1");

        var result = await um.ChangePasswordAsync(user, "OldPass1", "NewPass2");
        Assert.True(result.Succeeded);
    }
}
