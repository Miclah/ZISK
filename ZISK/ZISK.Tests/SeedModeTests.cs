using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZISK.Data;
using ZISK.Services;

namespace ZISK.Tests;

public class SeedModeTests
{
    private static (IServiceScope scope, DatabaseInitializer initializer, UserManager<ApplicationUser> um, ApplicationDbContext db)
        Build(string dbName, Dictionary<string, string?> configValues)
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

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
        services.AddSingleton(configuration);
        services.Configure<SeedPasswordOptions>(configuration.GetSection("Seed:Passwords"));
        services.Configure<SeedInitialAdminOptions>(configuration.GetSection("Seed:InitialAdmin"));
        services.AddScoped<DatabaseInitializer>();

        var sp = services.BuildServiceProvider();
        var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();

        return (scope, scope.ServiceProvider.GetRequiredService<DatabaseInitializer>(),
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(), db);
    }

    [Fact]
    public async Task LocalMode_Default_SeedsHardcodedCoreAccounts()
    {
        var (_, initializer, um, _) = Build(nameof(LocalMode_Default_SeedsHardcodedCoreAccounts), new());

        await initializer.SeedAsync();

        var admin = await um.FindByEmailAsync("admin@zisk.sk");
        Assert.NotNull(admin);
        Assert.True(await um.CheckPasswordAsync(admin!, "Admin1234"));
        Assert.Contains("Admin", await um.GetRolesAsync(admin!));
    }

    [Fact]
    public async Task DemoMode_MissingPasswordConfig_ThrowsSeedConfigurationException()
    {
        var (_, initializer, _, _) = Build(
            nameof(DemoMode_MissingPasswordConfig_ThrowsSeedConfigurationException),
            new() { ["ZISK_SEED_MODE"] = "demo" });

        var ex = await Assert.ThrowsAsync<SeedConfigurationException>(() => initializer.SeedAsync());
        Assert.Contains("Seed:Passwords:Admin", ex.Message);
    }

    [Fact]
    public async Task DemoMode_WithPasswordConfig_SeedsCoreAccountsWithConfiguredPasswords()
    {
        var (_, initializer, um, _) = Build(
            nameof(DemoMode_WithPasswordConfig_SeedsCoreAccountsWithConfiguredPasswords),
            new()
            {
                ["ZISK_SEED_MODE"] = "demo",
                ["Seed:Passwords:Admin"] = "DemoAdminPw1",
                ["Seed:Passwords:Coach"] = "DemoCoachPw1",
                ["Seed:Passwords:Parent"] = "DemoParentPw1",
                ["Seed:Passwords:Child"] = "DemoChildPw1"
            });

        await initializer.SeedAsync();

        var admin = await um.FindByEmailAsync("admin@zisk.sk");
        Assert.NotNull(admin);
        Assert.True(await um.CheckPasswordAsync(admin!, "DemoAdminPw1"));
        Assert.False(await um.CheckPasswordAsync(admin!, "Admin1234"));

        var sampleCoach = await um.FindByEmailAsync("rastislav.horvath@zisk.sk");
        Assert.NotNull(sampleCoach);
        Assert.True(await um.CheckPasswordAsync(sampleCoach!, "DemoCoachPw1"));
    }

    [Fact]
    public async Task ProductionMode_MissingAdminConfig_ThrowsSeedConfigurationException()
    {
        var (_, initializer, _, _) = Build(
            nameof(ProductionMode_MissingAdminConfig_ThrowsSeedConfigurationException),
            new() { ["ZISK_SEED_MODE"] = "production" });

        var ex = await Assert.ThrowsAsync<SeedConfigurationException>(() => initializer.SeedAsync());
        Assert.Contains("Seed:InitialAdmin", ex.Message);
    }

    [Fact]
    public async Task ProductionMode_CreatesOnlyInitialAdmin_NoDemoOrSampleData()
    {
        var (_, initializer, um, db) = Build(
            nameof(ProductionMode_CreatesOnlyInitialAdmin_NoDemoOrSampleData),
            new()
            {
                ["ZISK_SEED_MODE"] = "production",
                ["Seed:InitialAdmin:Email"] = "owner@realclub.sk",
                ["Seed:InitialAdmin:Password"] = "RealAdminPw1"
            });

        await initializer.SeedAsync();

        var admin = await um.FindByEmailAsync("owner@realclub.sk");
        Assert.NotNull(admin);
        Assert.True(await um.CheckPasswordAsync(admin!, "RealAdminPw1"));
        Assert.Contains("Admin", await um.GetRolesAsync(admin!));

        Assert.Null(await um.FindByEmailAsync("admin@zisk.sk"));
        Assert.Null(await um.FindByEmailAsync("rodic@zisk.sk"));
        Assert.False(await db.Teams.AnyAsync());
    }

    [Fact]
    public async Task UnknownSeedMode_ThrowsSeedConfigurationException()
    {
        var (_, initializer, _, _) = Build(
            nameof(UnknownSeedMode_ThrowsSeedConfigurationException),
            new() { ["ZISK_SEED_MODE"] = "staging" });

        await Assert.ThrowsAsync<SeedConfigurationException>(() => initializer.SeedAsync());
    }
}
