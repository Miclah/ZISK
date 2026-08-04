using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Services;

namespace ZISK.Tests;

/// <summary>
/// Guards the qualities that make the seeded data usable as a public demo rather than just
/// technically present: nobody is missing the profile fields the admin screens display, no player
/// is missing a parent (which renders as "kontakt na rodiča nie je k dispozícii" in the coach's
/// roster and reads as a bug), and the weekly plan behind the calendar actually exists.
/// </summary>
public class SeedDataCompletenessTests
{
    private static (DatabaseInitializer initializer, UserManager<ApplicationUser> um, ApplicationDbContext db)
        Build(string dbName)
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

        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        services.AddSingleton(configuration);
        services.Configure<SeedPasswordOptions>(configuration.GetSection("Seed:Passwords"));
        services.Configure<SeedInitialAdminOptions>(configuration.GetSection("Seed:InitialAdmin"));
        services.AddScoped<DatabaseInitializer>();

        var scope = services.BuildServiceProvider().CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();

        return (scope.ServiceProvider.GetRequiredService<DatabaseInitializer>(),
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            db);
    }

    [Fact]
    public async Task EverySeededUser_HasPhoneBirthNumberAddressAndDateOfBirth()
    {
        var (initializer, _, db) = Build(nameof(EverySeededUser_HasPhoneBirthNumberAddressAndDateOfBirth));

        await initializer.SeedAsync();

        var incomplete = await db.Users
            .Where(u => u.PhoneNumber == null || u.RodneCislo == null || u.Bydlisko == null || u.DateOfBirth == null)
            .Select(u => u.Email)
            .ToListAsync();

        Assert.Empty(incomplete);
    }

    [Fact]
    public async Task SeededPhoneNumbersAndBirthNumbers_AreUnique()
    {
        // Both columns carry a filtered unique index, so a duplicate here is not a cosmetic problem:
        // it fails the whole seed against SQL Server while passing against InMemory.
        var (initializer, _, db) = Build(nameof(SeededPhoneNumbersAndBirthNumbers_AreUnique));

        await initializer.SeedAsync();

        var users = await db.Users.ToListAsync();

        Assert.Equal(users.Count, users.Select(u => u.PhoneNumber).Distinct().Count());
        Assert.Equal(users.Count, users.Select(u => u.RodneCislo).Distinct().Count());
    }

    [Fact]
    public async Task SeededBirthNumbers_MatchTheirDateOfBirth_AndFailTheModulo11Check()
    {
        var (initializer, _, db) = Build(nameof(SeededBirthNumbers_MatchTheirDateOfBirth_AndFailTheModulo11Check));

        await initializer.SeedAsync();

        foreach (var user in await db.Users.ToListAsync())
        {
            var dob = user.DateOfBirth!.Value;
            var rc = user.RodneCislo!;
            var digits = rc.Replace("/", string.Empty);

            Assert.Equal($"{dob.Year % 100:D2}", rc[..2]);
            Assert.Equal($"{dob.Day:D2}", rc.Substring(4, 2));

            // Month carries the +50 offset for women, so it is either the real month or month + 50.
            var month = int.Parse(rc.Substring(2, 2));
            Assert.True(month == dob.Month || month == dob.Month + 50, $"{user.Email}: {rc} vs {dob}");

            // A real Slovak birth number issued after 1953 is divisible by 11. These must not be.
            Assert.NotEqual(0, long.Parse(digits) % 11);
        }
    }

    [Fact]
    public async Task EveryChildAndAthlete_HasAtLeastOneParent()
    {
        var (initializer, um, db) = Build(nameof(EveryChildAndAthlete_HasAtLeastOneParent));

        await initializer.SeedAsync();

        var players = (await um.GetUsersInRoleAsync("Child"))
            .Concat(await um.GetUsersInRoleAsync("Athlete"))
            .ToList();

        Assert.NotEmpty(players);

        var linkedChildIds = await db.ParentChildren.Select(pc => pc.ChildId).Distinct().ToListAsync();
        var orphans = players.Where(p => !linkedChildIds.Contains(p.Id)).Select(p => p.Email).ToList();

        Assert.Empty(orphans);
    }

    [Fact]
    public async Task RecurringSeries_AreSeeded_AndProduceTheCalendar()
    {
        var (initializer, _, db) = Build(nameof(RecurringSeries_AreSeeded_AndProduceTheCalendar));

        await initializer.SeedAsync();

        var series = await db.TrainingSeries.ToListAsync();
        Assert.NotEmpty(series);

        // Every active team needs a weekly plan, otherwise the "Opakované" tab looks half-filled.
        var teamsWithSeries = series.Select(s => s.TeamId).Distinct().Count();
        Assert.Equal(await db.Teams.CountAsync(t => t.IsActive), teamsWithSeries);

        Assert.All(series, s => Assert.NotEqual(0, s.DaysOfWeek));
        Assert.All(series, s => Assert.True(s.EndTime > s.StartTime));

        // The bulk of the calendar comes from those series, not from hand-written one-off rows.
        var seriesBacked = await db.TrainingEvents.CountAsync(te => te.SeriesId != null);
        Assert.True(seriesBacked > db.TrainingEvents.Count(te => te.SeriesId == null),
            "Most trainings should be generated from a series.");
    }

    [Fact]
    public async Task Reseeding_DoesNotDuplicateSeriesOrParentLinks()
    {
        var (initializer, _, db) = Build(nameof(Reseeding_DoesNotDuplicateSeriesOrParentLinks));

        await initializer.SeedAsync();
        var seriesAfterFirst = await db.TrainingSeries.CountAsync();
        var linksAfterFirst = await db.ParentChildren.CountAsync();

        await initializer.SeedAsync();

        Assert.Equal(seriesAfterFirst, await db.TrainingSeries.CountAsync());
        Assert.Equal(linksAfterFirst, await db.ParentChildren.CountAsync());
    }
}
