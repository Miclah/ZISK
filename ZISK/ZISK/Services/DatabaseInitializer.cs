using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ZISK.Data;
using ZISK.Data.Entities;

namespace ZISK.Services;

public class DatabaseInitializer
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<DatabaseInitializer> _logger;
    private readonly IConfiguration _configuration;
    private readonly SeedPasswordOptions _passwordOptions;
    private readonly SeedInitialAdminOptions _initialAdminOptions;
    private readonly IdentityOptions _identityOptions;

    public DatabaseInitializer(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ILogger<DatabaseInitializer> logger,
        IConfiguration configuration,
        IOptions<SeedPasswordOptions> passwordOptions,
        IOptions<SeedInitialAdminOptions> initialAdminOptions,
        IOptions<IdentityOptions> identityOptions)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
        _configuration = configuration;
        _passwordOptions = passwordOptions.Value;
        _initialAdminOptions = initialAdminOptions.Value;
        _identityOptions = identityOptions.Value;
    }

    private sealed record SeedPasswordSet(string Admin, string Coach, string Parent, string Child)
    {
        public static readonly SeedPasswordSet LocalDefault =
            new("Admin1234", "Trener1234", "Rodic1234", "Dieta1234");
    }

    private SeedPasswordSet ResolveDemoPasswordSet()
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(_passwordOptions.Admin)) missing.Add("Seed:Passwords:Admin");
        if (string.IsNullOrWhiteSpace(_passwordOptions.Coach)) missing.Add("Seed:Passwords:Coach");
        if (string.IsNullOrWhiteSpace(_passwordOptions.Parent)) missing.Add("Seed:Passwords:Parent");
        if (string.IsNullOrWhiteSpace(_passwordOptions.Child)) missing.Add("Seed:Passwords:Child");

        if (missing.Count > 0)
        {
            throw new SeedConfigurationException(
                $"ZISK_SEED_MODE=demo vyžaduje nasledujúce chýbajúce konfiguračné hodnoty: {string.Join(", ", missing)}.");
        }

        var set = new SeedPasswordSet(_passwordOptions.Admin!, _passwordOptions.Coach!, _passwordOptions.Parent!, _passwordOptions.Child!);
        ValidateAgainstPasswordPolicy(set);
        return set;
    }

    /// <summary>
    /// Fails the whole startup when a configured seed password cannot satisfy the Identity
    /// password policy. Without this, <see cref="EnsureUserAsync"/> just logs a warning per
    /// rejected user and carries on - a Seed:Passwords:Child value with no digit silently cost
    /// the deployed demo every child and athlete account (and with them the rosters, attendance
    /// and excuses built on top of them), while the site still looked like it had booted fine.
    /// </summary>
    private void ValidateAgainstPasswordPolicy(SeedPasswordSet set)
    {
        var policy = _identityOptions.Password;
        var problems = new List<string>();

        void Check(string settingName, string password)
        {
            var faults = new List<string>();
            if (password.Length < policy.RequiredLength) faults.Add($"aspoň {policy.RequiredLength} znakov");
            if (policy.RequireDigit && !password.Any(char.IsDigit)) faults.Add("aspoň jednu číslicu");
            if (policy.RequireLowercase && !password.Any(char.IsLower)) faults.Add("aspoň jedno malé písmeno");
            if (policy.RequireUppercase && !password.Any(char.IsUpper)) faults.Add("aspoň jedno veľké písmeno");
            if (policy.RequireNonAlphanumeric && password.All(char.IsLetterOrDigit)) faults.Add("aspoň jeden špeciálny znak");

            if (faults.Count > 0)
                problems.Add($"{settingName} (chýba: {string.Join(", ", faults)})");
        }

        Check("Seed:Passwords:Admin", set.Admin);
        Check("Seed:Passwords:Coach", set.Coach);
        Check("Seed:Passwords:Parent", set.Parent);
        Check("Seed:Passwords:Child", set.Child);

        if (problems.Count > 0)
        {
            throw new SeedConfigurationException(
                $"Nasledujúce seed heslá nespĺňajú politiku hesiel: {string.Join("; ", problems)}.");
        }
    }

    private async Task EnsureProductionAdminAsync()
    {
        if (string.IsNullOrWhiteSpace(_initialAdminOptions.Email) || string.IsNullOrWhiteSpace(_initialAdminOptions.Password))
        {
            throw new SeedConfigurationException(
                "ZISK_SEED_MODE=production vyžaduje konfiguráciu Seed:InitialAdmin:Email a Seed:InitialAdmin:Password.");
        }

        await EnsureUserAsync(
            _initialAdminOptions.Email, _initialAdminOptions.Password, "Admin",
            _initialAdminOptions.FirstName, _initialAdminOptions.LastName);
    }

    public async Task InitializeAsync()
    {
        await _context.Database.MigrateAsync();
        await SeedAsync();
    }

    /// <summary>
    /// The seed-mode branching logic, split out from <see cref="InitializeAsync"/> so it can be
    /// exercised against EF Core InMemory in tests (InMemory does not support MigrateAsync).
    /// </summary>
    public async Task SeedAsync()
    {
        await SeedRolesAsync();

        var mode = SeedModeResolver.Resolve(_configuration);

        if (mode == SeedMode.Production)
        {
            // No demo/sample data in production — just roles + a single real admin account.
            await EnsureProductionAdminAsync();
            return;
        }

        var passwords = mode == SeedMode.Demo ? ResolveDemoPasswordSet() : SeedPasswordSet.LocalDefault;

        var anchor = DateTime.UtcNow;

        // Relative to seed time rather than a fixed year, so a demo deployed years from now does
        // not have an 11-year-old who is visibly 20. The day offset keeps the generated dates off
        // the seeding date itself, which would otherwise be the same day and month for everyone.
        var anchorDate = DateOnly.FromDateTime(anchor);
        DateOnly BirthDate(int ageYears, int dayOffset) => anchorDate.AddYears(-ageYears).AddDays(-dayOffset);

        var admin  = await EnsureUserAsync("admin@zisk.sk",  passwords.Admin,  "Admin",  "Miroslav", "Kráľ",    BirthDate(44, 137));
        var coach  = await EnsureUserAsync("trener@zisk.sk", passwords.Coach,  "Coach",  "Marek",    "Kováčik", BirthDate(38, 291));
        var parent = await EnsureUserAsync("rodic@zisk.sk",  passwords.Parent, "Parent", "Peter",    "Novák",   BirthDate(41, 58));
        var child  = await EnsureUserAsync("dieta@zisk.sk",  passwords.Child,  "Child",  "Tomáš",    "Novák",   BirthDate(12, 96));

        await SeedTeamsAsync();
        await EnsureDefaultSeasonAsync(anchor);
        await EnsureChildSeedUserAsync(child, parent);
        await EnsureCoachTeamAssignmentsAsync(coach);
        await SeedSampleDataAsync(admin, coach, parent, child, passwords, anchor);

        if (mode == SeedMode.Demo)
        {
            // Marks "now" as the template's freshness baseline. Without this, the very first
            // hourly RefreshDemoTemplateAsync pass would find no DemoTemplateMeta row, treat
            // the template as infinitely stale, and immediately delete+regenerate the data
            // this method just seeded.
            var meta = await _context.DemoTemplateMetas.FirstOrDefaultAsync(m => m.Id == 1);
            await SaveTemplateMetaAsync(meta, anchor);
        }
    }

    /// <summary>
    /// Regenerates the shared demo template's date-bound content (trainings, attendance,
    /// absence requests, announcements, documents) anchored to "now", so a public demo
    /// deployment never accumulates a calendar's worth of trainings sitting entirely in the
    /// past. Called once at startup and then daily by DemoTemplateRefreshWorker. A no-op
    /// outside ZISK_SEED_MODE=demo, and a no-op if the template was already refreshed within
    /// the last 24 hours. Only ever touches DemoSessionId == null rows (the template) -
    /// already-cloned visitor sessions are untouched, which the ambient query filter enforces
    /// automatically since this always runs outside an HTTP request (DemoSessionId is null).
    /// </summary>
    public async Task RefreshDemoTemplateAsync()
    {
        if (SeedModeResolver.Resolve(_configuration) != SeedMode.Demo)
            return;

        var anchor = DateTime.UtcNow;
        var meta = await _context.DemoTemplateMetas.FirstOrDefaultAsync(m => m.Id == 1);
        if (meta != null && anchor - meta.GeneratedAt < TimeSpan.FromHours(24))
            return;

        _logger.LogInformation("Demo template data is stale (last generated {GeneratedAt:u}); regenerating.", meta?.GeneratedAt);

        // Delete only the date-bound content, not teams/users/season - those don't need to be
        // recreated daily, just occasionally nudged back into range (EnsureDefaultSeasonAsync
        // below). Loaded + RemoveRange rather than ExecuteDeleteAsync so this stays exercisable
        // against EF Core InMemory in tests, which doesn't support ExecuteDelete at all; at
        // template scale (a few hundred rows) the extra round trip is not a real cost.
        _context.AttendanceRecords.RemoveRange(await _context.AttendanceRecords.Where(x => x.DemoSessionId == null).ToListAsync());
        _context.AbsenceRequests.RemoveRange(await _context.AbsenceRequests.Where(x => x.DemoSessionId == null).ToListAsync());
        _context.AnnouncementAttachments.RemoveRange(await _context.AnnouncementAttachments.Where(x => x.DemoSessionId == null).ToListAsync());
        _context.Announcements.RemoveRange(await _context.Announcements.Where(x => x.DemoSessionId == null).ToListAsync());
        _context.Documents.RemoveRange(await _context.Documents.Where(x => x.DemoSessionId == null).ToListAsync());
        _context.TrainingEvents.RemoveRange(await _context.TrainingEvents.Where(x => x.DemoSessionId == null).ToListAsync());
        await _context.SaveChangesAsync();

        await EnsureDefaultSeasonAsync(anchor);

        var teams = await _context.Teams.Where(t => t.IsActive).OrderBy(t => t.Name).ToListAsync();
        if (teams.Count == 0)
        {
            await SaveTemplateMetaAsync(meta, anchor);
            return;
        }

        var admin  = await _userManager.FindByEmailAsync("admin@zisk.sk");
        var coach  = await _userManager.FindByEmailAsync("trener@zisk.sk");
        var parent = await _userManager.FindByEmailAsync("rodic@zisk.sk");

        var teamIds = teams.Select(t => t.Id).ToHashSet();
        var sampleChildren = await _context.TeamMembers
            .Where(tm => teamIds.Contains(tm.TeamId))
            .Select(tm => tm.User)
            .Distinct()
            .ToListAsync();

        var sampleTrainings = await EnsureSampleTrainingsAsync(teams, anchor);
        var attendanceUser  = coach ?? admin;

        await EnsureSampleAttendanceAsync(sampleTrainings, sampleChildren, attendanceUser);
        await EnsureSampleAbsenceRequestsAsync(sampleTrainings, sampleChildren, parent);

        // The weekly plan survives the delete pass above, so this normally just reads the existing
        // series back and re-expands them against the refreshed anchor.
        var sampleSeries = await EnsureSampleTrainingSeriesAsync(teams);
        await EnsureRichSampleHistoryAsync(teams, sampleSeries, sampleChildren, attendanceUser, parent, anchor);

        var announcementAuthor = admin ?? coach;
        await EnsureSampleAnnouncementsAsync(announcementAuthor, teams, anchor);
        await EnsureSampleDocumentsAsync(admin, anchor);

        await SaveTemplateMetaAsync(meta, anchor);
    }

    private async Task SaveTemplateMetaAsync(DemoTemplateMeta? existing, DateTime anchor)
    {
        if (existing == null)
            _context.DemoTemplateMetas.Add(new DemoTemplateMeta { Id = 1, GeneratedAt = anchor });
        else
            existing.GeneratedAt = anchor;

        await _context.SaveChangesAsync();
    }

    private sealed record SampleUserGroup(
        List<ApplicationUser> Coaches,
        List<ApplicationUser> Parents,
        List<ApplicationUser> Athletes,
        List<ApplicationUser> Children);

    private async Task SeedRolesAsync()
    {
        string[] roles = ["Admin", "Coach", "Parent", "Athlete", "Child"];
        foreach (var role in roles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
                await _roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    private async Task EnsureDefaultSeasonAsync(DateTime anchor)
    {
        var anchorDate = DateOnly.FromDateTime(anchor);
        var season = await _context.Seasons.FirstOrDefaultAsync(s => s.IsActive);

        if (season == null)
        {
            _context.Seasons.Add(new Season
            {
                Id        = Guid.NewGuid(),
                Name      = $"Sezóna {anchor.Year}/{anchor.Year + 1}",
                StartDate = anchorDate.AddMonths(-6),
                EndDate   = anchorDate.AddMonths(6),
                IsActive  = true,
                CreatedAt = anchor
            });
        }
        else if (anchorDate < season.StartDate || anchorDate > season.EndDate)
        {
            // The active season's window has drifted out of range (e.g. the app was
            // redeployed a year later) - slide it forward instead of leaving every training
            // generated below with a SeasonId that no longer covers "now".
            season.Name      = $"Sezóna {anchor.Year}/{anchor.Year + 1}";
            season.StartDate = anchorDate.AddMonths(-6);
            season.EndDate   = anchorDate.AddMonths(6);
        }

        await _context.SaveChangesAsync();
    }

    private async Task EnsureChildSeedUserAsync(ApplicationUser? childUser, ApplicationUser? parent)
    {
        if (childUser == null)
            return;

        var hasTeam = await _context.TeamMembers.AnyAsync(tm => tm.UserId == childUser.Id);
        if (!hasTeam)
        {
            // Tomáš Novák patrí do Prípravky
            var teamId = await _context.Teams
                .Where(t => t.IsActive && t.Name == "Prípravka")
                .Select(t => (Guid?)t.Id)
                .FirstOrDefaultAsync()
                ?? await _context.Teams
                    .Where(t => t.IsActive)
                    .OrderBy(t => t.Name)
                    .Select(t => (Guid?)t.Id)
                    .FirstOrDefaultAsync();

            if (teamId.HasValue)
            {
                _context.TeamMembers.Add(new TeamMember
                {
                    TeamId   = teamId.Value,
                    UserId   = childUser.Id,
                    JoinedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }
        }

        if (parent != null)
        {
            var hasLink = await _context.ParentChildren
                .AnyAsync(pc => pc.ParentId == parent.Id && pc.ChildId == childUser.Id);

            if (!hasLink)
            {
                _context.ParentChildren.Add(new ParentChild
                {
                    ParentId  = parent.Id,
                    ChildId   = childUser.Id,
                    IsPrimary = true
                });
                await _context.SaveChangesAsync();
            }
        }
    }

    private async Task<ApplicationUser?> EnsureUserAsync(
        string email, string password, string role,
        string firstName, string lastName,
        DateOnly? dateOfBirth = null)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName       = email,
                Email          = email,
                FirstName      = firstName,
                LastName       = lastName,
                DateOfBirth    = dateOfBirth,
                EmailConfirmed = true,
                IsActive       = true
            };

            ApplyDemoProfileDetails(user, email, dateOfBirth);

            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                _logger.LogWarning("Unable to create seed user {Email}: {Error}",
                    email, string.Join(", ", result.Errors.Select(e => e.Description)));
                return await _userManager.FindByEmailAsync(email);
            }
        }
        else if (ApplyDemoProfileDetails(user, email, dateOfBirth))
        {
            // Backfill for accounts that predate these fields. A demo deployment keeps its user
            // rows across restarts, so filling the profile in only at creation time would leave
            // every already-deployed demo with the empty phone/birth-number/address columns this
            // is meant to remove.
            await _userManager.UpdateAsync(user);
        }

        if (!await _userManager.IsInRoleAsync(user, role))
            await _userManager.AddToRoleAsync(user, role);

        return user;
    }

    /// <summary>
    /// Fills in the fictional phone number, birth number and address that make the demo read like a
    /// real club register. Only accounts listed in <see cref="SeedPersonDirectory"/> are touched, so
    /// a production admin never gets made-up data, and only blank fields are written, so anything a
    /// demo visitor edits survives the next startup. Returns true when something changed.
    /// </summary>
    private static bool ApplyDemoProfileDetails(ApplicationUser user, string email, DateOnly? dateOfBirth)
    {
        var person = SeedPersonDirectory.Find(email);
        if (person is null)
            return false;

        var changed = false;

        if (user.DateOfBirth is null && dateOfBirth is not null)
        {
            user.DateOfBirth = dateOfBirth;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            user.PhoneNumber = person.Phone;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(user.Bydlisko))
        {
            user.Bydlisko = person.Address;
            changed = true;
        }

        // Derived from the birth date that is already on the account, so the number a coach sees
        // always agrees with the date of birth shown next to it.
        if (string.IsNullOrWhiteSpace(user.RodneCislo) && user.DateOfBirth is { } dob)
        {
            user.RodneCislo = SeedPersonDirectory.BuildRodneCislo(dob, person.IsFemale, person.Serial);
            changed = true;
        }

        return changed;
    }

    private async Task SeedTeamsAsync()
    {
        if (await _context.Teams.AnyAsync())
            return;

        _context.Teams.AddRange(
            new Team { Id = Guid.NewGuid(), Name = "A-tím",     ShortName = "A", Description = "Seniorský tím – III. liga, hlavná súťaž",       IsActive = true },
            new Team { Id = Guid.NewGuid(), Name = "B-tím",     ShortName = "B", Description = "Záložný seniorský tím – prípravné zápasy",       IsActive = true },
            new Team { Id = Guid.NewGuid(), Name = "Žiaci",     ShortName = "Ž", Description = "Mládežnícka kategória U15",                      IsActive = true },
            new Team { Id = Guid.NewGuid(), Name = "Prípravka", ShortName = "P", Description = "Najmladší hráči, kategória U11",                 IsActive = true }
        );
        await _context.SaveChangesAsync();
    }

    private async Task EnsureCoachTeamAssignmentsAsync(ApplicationUser? coach)
    {
        if (coach == null)
            return;

        // Marek Kováčik je primárny tréner A-tímu
        var aTeamId = await _context.Teams
            .Where(t => t.IsActive && t.Name == "A-tím")
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync();

        if (!aTeamId.HasValue)
            return;

        var exists = await _context.CoachTeams
            .AnyAsync(ct => ct.CoachId == coach.Id && ct.TeamId == aTeamId.Value);

        if (!exists)
        {
            _context.CoachTeams.Add(new CoachTeam
            {
                Id         = Guid.NewGuid(),
                CoachId    = coach.Id,
                TeamId     = aTeamId.Value,
                IsPrimary  = true,
                AssignedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
        }
    }

    private async Task SeedSampleDataAsync(
        ApplicationUser? admin,
        ApplicationUser? defaultCoach,
        ApplicationUser? defaultParent,
        ApplicationUser? coreChild,
        SeedPasswordSet passwords,
        DateTime anchor)
    {
        var teams = await _context.Teams
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync();

        if (!teams.Any())
            return;

        var sampleUsers   = await EnsureSampleUsersAsync(passwords, anchor);
        var sampleChildren = await EnsureSampleChildrenAsync(teams, sampleUsers);

        // Zahrnúť aj hlavného testovacie dieťa do zoznamu pre dochádzku
        if (coreChild != null && !sampleChildren.Any(c => c.Id == coreChild.Id))
            sampleChildren.Insert(0, coreChild);

        await EnsureSampleParentLinksAsync(defaultParent, sampleUsers, coreChild);
        await EnsureSampleCoachAssignmentsAsync(sampleUsers, teams);

        var sampleTrainings = await EnsureSampleTrainingsAsync(teams, anchor);
        var attendanceUser  = defaultCoach ?? sampleUsers.Coaches.FirstOrDefault() ?? admin;

        await EnsureSampleAttendanceAsync(sampleTrainings, sampleChildren, attendanceUser);

        var sampleParent = sampleUsers.Parents.FirstOrDefault() ?? defaultParent;
        await EnsureSampleAbsenceRequestsAsync(sampleTrainings, sampleChildren, sampleParent);

        var sampleSeries = await EnsureSampleTrainingSeriesAsync(teams);
        await EnsureRichSampleHistoryAsync(teams, sampleSeries, sampleChildren, attendanceUser, sampleParent, anchor);

        var announcementAuthor = admin ?? defaultCoach ?? sampleUsers.Coaches.FirstOrDefault();
        await EnsureSampleAnnouncementsAsync(announcementAuthor, teams, anchor);
        await EnsureSampleDocumentsAsync(admin, anchor);
    }

    private async Task<SampleUserGroup> EnsureSampleUsersAsync(SeedPasswordSet passwords, DateTime anchor)
    {
        var coaches  = new List<ApplicationUser>();
        var parents  = new List<ApplicationUser>();
        var athletes = new List<ApplicationUser>();
        var children = new List<ApplicationUser>();

        // Birth dates are computed relative to the seed anchor (age + a small deterministic
        // day offset for variety) rather than fixed years, so a demo deployed years from now
        // doesn't have "U15" athletes who are visibly 30.
        var anchorDate = DateOnly.FromDateTime(anchor);
        DateOnly BirthDate(int ageYears, int dayOffset) => anchorDate.AddYears(-ageYears).AddDays(-dayOffset);

        // Tréneri
        foreach (var (email, fn, ln, dob) in new[]
        {
            ("rastislav.horvath@zisk.sk", "Rastislav", "Horváth", BirthDate(45, 172)),
            ("tomas.balaz@zisk.sk",       "Tomáš",     "Baláž",   BirthDate(36, 24)),
            ("jan.minac@zisk.sk",         "Ján",       "Mináč",   BirthDate(51, 309))
        })
        {
            var u = await EnsureUserAsync(email, passwords.Coach, "Coach", fn, ln, dob);
            if (u != null) coaches.Add(u);
        }

        // Rodičia
        foreach (var (email, fn, ln, dob) in new[]
        {
            ("jana.novakova@zisk.sk",   "Jana",     "Nováková", BirthDate(39, 214)),
            ("milan.horak@zisk.sk",     "Milan",    "Horák",    BirthDate(47, 96)),
            ("andrea.blahova@zisk.sk",  "Andrea",   "Bláhová",  BirthDate(43, 341)),
            ("lukas.kral@zisk.sk",      "Lukáš",    "Kráľ",     BirthDate(45, 63)),
            ("monika.simonova@zisk.sk", "Monika",   "Šimonová", BirthDate(42, 188)),
            ("juraj.balog@zisk.sk",     "Juraj",    "Balog",    BirthDate(46, 275)),
            ("zuzana.oravec@zisk.sk",   "Zuzana",   "Oravec",   BirthDate(40, 131)),
            ("eva.mala@zisk.sk",        "Eva",      "Malá",     BirthDate(44, 41)),
            ("ivan.varga@zisk.sk",      "Ivan",     "Varga",    BirthDate(48, 226)),
            ("marta.cierna@zisk.sk",    "Marta",    "Čierna",   BirthDate(41, 158)),
            ("vladimir.holub@zisk.sk",  "Vladimír", "Holúb",    BirthDate(39, 87))
        })
        {
            var u = await EnsureUserAsync(email, passwords.Parent, "Parent", fn, ln, dob);
            if (u != null) parents.Add(u);
        }

        // Starší športovci (Athlete) — A-tím a B-tím
        foreach (var (email, fn, ln, dob) in new[]
        {
            ("lukas.maly@zisk.sk",      "Lukáš",   "Malý",   BirthDate(22, 74)),
            ("martin.horak@zisk.sk",    "Martin",  "Horák",  BirthDate(21, 203)),
            ("jakub.blaha@zisk.sk",     "Jakub",   "Bláha",  BirthDate(22, 312)),
            ("adam.kral@zisk.sk",       "Adam",    "Kráľ",   BirthDate(21, 45)),
            ("michal.simon@zisk.sk",    "Michal",  "Šimon",  BirthDate(19, 123)),
            ("juraj.balog.jr@zisk.sk",  "Juraj",   "Balog",  BirthDate(20, 260)),
            ("richard.varga@zisk.sk",   "Richard", "Varga",  BirthDate(19, 25))
        })
        {
            var u = await EnsureUserAsync(email, passwords.Child, "Athlete", fn, ln, dob);
            if (u != null) athletes.Add(u);
        }

        // Mladší deti (Child) — Žiaci a Prípravka
        foreach (var (email, fn, ln, dob) in new[]
        {
            ("petra.horakova@zisk.sk",  "Petra",   "Horáková", BirthDate(16, 97)),
            ("klara.oravec@zisk.sk",    "Klára",   "Oravec",   BirthDate(15, 163)),
            ("filip.cerny@zisk.sk",     "Filip",   "Čierny",   BirthDate(16, 273)),
            ("zuzana.kralova@zisk.sk",  "Zuzana",  "Kráľová",  BirthDate(15, 76)),
            ("samuel.novak@zisk.sk",    "Samuel",  "Novák",    BirthDate(12, 232)),
            ("ema.holubova@zisk.sk",    "Ema",     "Holúbová", BirthDate(11, 9)),
            ("ondrej.maly@zisk.sk",     "Ondrej",  "Malý",     BirthDate(12, 307)),
            ("nina.blahova@zisk.sk",    "Nina",    "Bláhová",  BirthDate(11, 148))
        })
        {
            var u = await EnsureUserAsync(email, passwords.Child, "Child", fn, ln, dob);
            if (u != null) children.Add(u);
        }

        return new SampleUserGroup(coaches, parents, athletes, children);
    }

    private async Task<List<ApplicationUser>> EnsureSampleChildrenAsync(
        List<Team> teams, SampleUserGroup sampleUsers)
    {
        var result = new List<ApplicationUser>();
        var teamByName = teams.ToDictionary(t => t.Name, t => t.Id);

        var aTeamId        = teamByName.GetValueOrDefault("A-tím",     teams[0].Id);
        var bTeamId        = teamByName.GetValueOrDefault("B-tím",     teams.Count > 1 ? teams[1].Id : teams[0].Id);
        var ziaciTeamId    = teamByName.GetValueOrDefault("Žiaci",     teams.Count > 2 ? teams[2].Id : teams[0].Id);
        var pripravkaTeamId = teamByName.GetValueOrDefault("Prípravka", teams.Count > 3 ? teams[3].Id : teams[0].Id);

        // A-tím: prví 4 športovci
        var aTeamAthletes = sampleUsers.Athletes.Take(4).ToList();
        foreach (var u in aTeamAthletes)
            await EnsureTeamMembership(u, aTeamId, result);

        // B-tím: ďalší 3 športovci
        var bTeamAthletes = sampleUsers.Athletes.Skip(4).Take(3).ToList();
        foreach (var u in bTeamAthletes)
            await EnsureTeamMembership(u, bTeamId, result);

        // Žiaci: prvé 4 deti
        var ziaciChildren = sampleUsers.Children.Take(4).ToList();
        foreach (var u in ziaciChildren)
            await EnsureTeamMembership(u, ziaciTeamId, result);

        // Prípravka: ďalšie 4 deti
        var pripravkaChildren = sampleUsers.Children.Skip(4).Take(4).ToList();
        foreach (var u in pripravkaChildren)
            await EnsureTeamMembership(u, pripravkaTeamId, result);

        await _context.SaveChangesAsync();
        return result;
    }

    private async Task EnsureTeamMembership(ApplicationUser user, Guid teamId, List<ApplicationUser> result)
    {
        var hasMembership = await _context.TeamMembers
            .AnyAsync(tm => tm.UserId == user.Id && tm.TeamId == teamId);

        if (!hasMembership)
        {
            _context.TeamMembers.Add(new TeamMember
            {
                TeamId   = teamId,
                UserId   = user.Id,
                JoinedAt = DateTime.UtcNow
            });
        }

        if (!result.Any(r => r.Id == user.Id))
            result.Add(user);
    }

    private async Task EnsureSampleParentLinksAsync(
        ApplicationUser? defaultParent,
        SampleUserGroup sampleUsers,
        ApplicationUser? coreChild)
    {
        // Lookup parents by email
        async Task<ApplicationUser?> FindParent(string email) =>
            await _userManager.FindByEmailAsync(email);

        async Task<ApplicationUser?> FindChild(string email) =>
            await _userManager.FindByEmailAsync(email);

        // Every child and athlete must appear here at least once. A player without a parent shows up
        // in the coach's roster as "kontakt na rodiča nie je k dispozícii", which in a demo reads as
        // a bug rather than as an intentionally empty state.
        var links = new List<(string ParentEmail, string ChildEmail, bool IsPrimary)>
        {
            // Novákovci: otec Peter (primárny, linknutý cez EnsureChildSeedUserAsync), mama Jana
            ("jana.novakova@zisk.sk",  "dieta@zisk.sk",          false),
            ("rodic@zisk.sk",          "samuel.novak@zisk.sk",   true),
            ("jana.novakova@zisk.sk",  "samuel.novak@zisk.sk",   false),
            // Horákovci: Martin a Petra
            ("milan.horak@zisk.sk",    "martin.horak@zisk.sk",   true),
            ("milan.horak@zisk.sk",    "petra.horakova@zisk.sk", true),
            // Bláhovci: Jakub a Nina
            ("andrea.blahova@zisk.sk", "jakub.blaha@zisk.sk",    true),
            ("andrea.blahova@zisk.sk", "nina.blahova@zisk.sk",   true),
            // Kráľovci: Adam a Zuzana
            ("lukas.kral@zisk.sk",     "adam.kral@zisk.sk",      true),
            ("lukas.kral@zisk.sk",     "zuzana.kralova@zisk.sk", true),
            // Šimonovci: Michal
            ("monika.simonova@zisk.sk","michal.simon@zisk.sk",   true),
            // Balogovci: Juraj ml.
            ("juraj.balog@zisk.sk",    "juraj.balog.jr@zisk.sk", true),
            // Oravcovci: Klára
            ("zuzana.oravec@zisk.sk",  "klara.oravec@zisk.sk",   true),
            // Malí: Lukáš a Ondrej, súrodenci naprieč A-tímom a Prípravkou
            ("eva.mala@zisk.sk",       "lukas.maly@zisk.sk",     true),
            ("eva.mala@zisk.sk",       "ondrej.maly@zisk.sk",    true),
            // Vargovci: Richard
            ("ivan.varga@zisk.sk",     "richard.varga@zisk.sk",  true),
            // Čierni: Filip
            ("marta.cierna@zisk.sk",   "filip.cerny@zisk.sk",    true),
            // Holúbovci: Ema
            ("vladimir.holub@zisk.sk", "ema.holubova@zisk.sk",   true)
        };

        foreach (var (parentEmail, childEmail, isPrimary) in links)
        {
            var p = await FindParent(parentEmail);
            var c = await FindChild(childEmail);
            if (p == null || c == null) continue;

            var exists = await _context.ParentChildren
                .AnyAsync(pc => pc.ParentId == p.Id && pc.ChildId == c.Id);

            if (!exists)
            {
                _context.ParentChildren.Add(new ParentChild
                {
                    ParentId  = p.Id,
                    ChildId   = c.Id,
                    IsPrimary = isPrimary
                });
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task EnsureSampleCoachAssignmentsAsync(SampleUserGroup sampleUsers, List<Team> teams)
    {
        var teamByName = teams.ToDictionary(t => t.Name, t => t.Id);

        // Každý tréner má primárny tím
        var assignments = new List<(string CoachEmail, string TeamName, bool IsPrimary)>
        {
            ("rastislav.horvath@zisk.sk", "B-tím",     true),
            ("tomas.balaz@zisk.sk",       "Žiaci",     true),
            ("jan.minac@zisk.sk",         "Prípravka", true)
        };

        foreach (var (coachEmail, teamName, isPrimary) in assignments)
        {
            var coach = await _userManager.FindByEmailAsync(coachEmail);
            if (coach == null) continue;

            if (!teamByName.TryGetValue(teamName, out var teamId)) continue;

            var exists = await _context.CoachTeams
                .AnyAsync(ct => ct.CoachId == coach.Id && ct.TeamId == teamId);

            if (exists) continue;

            var hasPrimary = await _context.CoachTeams
                .AnyAsync(ct => ct.CoachId == coach.Id && ct.IsPrimary);

            _context.CoachTeams.Add(new CoachTeam
            {
                Id         = Guid.NewGuid(),
                CoachId    = coach.Id,
                TeamId     = teamId,
                IsPrimary  = isPrimary && !hasPrimary,
                AssignedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
    }

    private async Task<List<TrainingEvent>> EnsureSampleTrainingsAsync(List<Team> teams, DateTime anchor)
    {
        // Ak už existujú tréningy, preskočíme (rich history ich vytvorí neskôr)
        if (await _context.TrainingEvents.AnyAsync())
            return [];

        var activeSeason = await _context.Seasons.FirstOrDefaultAsync(s => s.IsActive);
        if (activeSeason == null)
            return [];

        var teamByName = teams.ToDictionary(t => t.Name, t => t);
        var now = anchor;

        var trainings = new List<TrainingEvent>
        {
            new()
            {
                Id        = Guid.NewGuid(),
                TeamId    = teamByName.GetValueOrDefault("A-tím", teams[0]).Id,
                SeasonId  = activeSeason.Id,
                Title     = "Kondičný tréning – A",
                StartTime = now.AddDays(-3).Date.AddHours(17),
                EndTime   = now.AddDays(-3).Date.AddHours(18).AddMinutes(30),
                Location  = "Hlavná telocvičňa",
                Type      = TrainingType.Conditioning,
                CoachNote = "Zameranie na rýchlosť, reakčný čas a mobilitu.",
                CreatedAt = now.AddDays(-10)
            },
            new()
            {
                Id        = Guid.NewGuid(),
                TeamId    = teamByName.GetValueOrDefault("B-tím", teams.Count > 1 ? teams[1] : teams[0]).Id,
                SeasonId  = activeSeason.Id,
                Title     = "Technika a prihrávky – B",
                StartTime = now.AddDays(-1).Date.AddHours(16),
                EndTime   = now.AddDays(-1).Date.AddHours(17).AddMinutes(30),
                Location  = "Vedľajšia telocvičňa",
                Type      = TrainingType.Technical,
                CoachNote = "Práca s loptou, krátke prihrávky, 1 na 1.",
                CreatedAt = now.AddDays(-8)
            },
            new()
            {
                Id        = Guid.NewGuid(),
                TeamId    = teamByName.GetValueOrDefault("Žiaci", teams.Count > 3 ? teams[3] : teams[0]).Id,
                SeasonId  = activeSeason.Id,
                Title     = "Zápasová simulácia – Ž",
                StartTime = now.AddDays(2).Date.AddHours(17),
                EndTime   = now.AddDays(2).Date.AddHours(18).AddMinutes(45),
                Location  = "Štadión – hlavné ihrisko",
                Type      = TrainingType.Match,
                CoachNote = "Modelové herné situácie a rohové kopy.",
                CreatedAt = now.AddDays(-5)
            }
        };

        _context.TrainingEvents.AddRange(trainings);
        await _context.SaveChangesAsync();
        return trainings;
    }

    private async Task EnsureSampleAttendanceAsync(
        List<TrainingEvent> trainings,
        List<ApplicationUser> sampleChildren,
        ApplicationUser? markedByUser)
    {
        var pastTrainings = trainings
            .Where(t => t.StartTime <= DateTime.UtcNow.AddHours(-1))
            .ToList();

        foreach (var training in pastTrainings)
        {
            var teamMemberIds = await _context.TeamMembers
                .Where(tm => tm.TeamId == training.TeamId
                          && sampleChildren.Select(c => c.Id).Contains(tm.UserId))
                .Select(tm => tm.UserId)
                .ToListAsync();

            for (var i = 0; i < teamMemberIds.Count; i++)
            {
                var childId = teamMemberIds[i];
                var exists  = await _context.AttendanceRecords
                    .AnyAsync(ar => ar.TrainingEventId == training.Id && ar.ChildId == childId);

                if (exists) continue;

                var status = (i % 3) switch
                {
                    0 => AttendanceStatus.Present,
                    1 => AttendanceStatus.Absent,
                    _ => AttendanceStatus.Excused
                };

                _context.AttendanceRecords.Add(new AttendanceRecord
                {
                    Id              = Guid.NewGuid(),
                    TrainingEventId = training.Id,
                    ChildId         = childId,
                    Status          = status,
                    Note            = status == AttendanceStatus.Absent ? "Krátkodobá absencia" : null,
                    CoachComment    = status == AttendanceStatus.Present ? "Dobrý výkon na tréningu." : null,
                    MarkedByUserId  = markedByUser?.Id,
                    RecordedAt      = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task EnsureSampleAbsenceRequestsAsync(
        List<TrainingEvent> trainings,
        List<ApplicationUser> sampleChildren,
        ApplicationUser? parent)
    {
        if (parent == null)
            return;

        var upcomingTraining = trainings
            .Where(t => t.StartTime > DateTime.UtcNow)
            .OrderBy(t => t.StartTime)
            .FirstOrDefault();

        if (upcomingTraining == null)
            return;

        var childId = await _context.TeamMembers
            .Where(tm => tm.TeamId == upcomingTraining.TeamId
                      && sampleChildren.Select(c => c.Id).Contains(tm.UserId))
            .Select(tm => tm.UserId)
            .FirstOrDefaultAsync();

        if (childId == null)
            return;

        var existsAlready = await _context.AbsenceRequests
            .AnyAsync(ar => ar.ParentId == parent.Id
                         && ar.ChildId == childId
                         && ar.TrainingEventId == upcomingTraining.Id);

        if (existsAlready)
            return;

        _context.AbsenceRequests.Add(new AbsenceRequest
        {
            Id              = Guid.NewGuid(),
            ChildId         = childId,
            ParentId        = parent.Id,
            TrainingEventId = upcomingTraining.Id,
            DateFrom        = upcomingTraining.StartTime,
            DateTo          = upcomingTraining.EndTime,
            Reason          = "Školský výlet – mimoškolská aktivita",
            Note            = "Návrat na ďalší tréning podľa plánu.",
            Status          = AbsenceRequestStatus.Received,
            CreatedAt       = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Creates the weekly plan each team trains on. These rows are what the "Opakované" tab shows,
    /// and <see cref="EnsureRichSampleHistoryAsync"/> expands them into the calendar, so every
    /// recurring training in the demo belongs to a series a visitor can open, edit and regenerate.
    /// <para>
    /// Deliberately left out of <see cref="RefreshDemoTemplateAsync"/>'s delete pass: a weekly plan
    /// is not date-bound, only the instances generated from it are. It is also what keeps
    /// <see cref="TrainingSeriesGeneratorWorker"/> extending the calendar on its own after the seed.
    /// </para>
    /// </summary>
    private async Task<List<TrainingSeries>> EnsureSampleTrainingSeriesAsync(List<Team> teams)
    {
        var existing = await _context.TrainingSeries
            .Include(ts => ts.Season)
            .Where(ts => ts.IsActive)
            .ToListAsync();

        if (existing.Count > 0)
            return existing;

        var activeSeason = await _context.Seasons.FirstOrDefaultAsync(s => s.IsActive);
        if (activeSeason == null)
            return [];

        var teamByName = teams.ToDictionary(t => t.Name, t => t);

        // A series needs a coach (CoachId is a Restrict FK), so resolve each team's primary coach and
        // fall back to any coach at all rather than dropping the series.
        var coachTeams = await _context.CoachTeams.ToListAsync();
        var coachByTeam = coachTeams
            .GroupBy(ct => ct.TeamId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(ct => ct.IsPrimary).First().CoachId);

        var fallbackCoachId = (await _userManager.GetUsersInRoleAsync("Coach")).FirstOrDefault()?.Id;

        var templates = new (string TeamName, string Title, Weekdays Days, int Hour, int Minute, int Minutes, string Location, TrainingType Type, string Note)[]
        {
            ("A-tím",     "Kondičný tréning – A",     Weekdays.Monday,    17, 0, 90, "Posilňovňa",             TrainingType.Conditioning, "Sila, výbušnosť a core. Vlastné rukavice so sebou."),
            ("A-tím",     "Herné situácie – A",       Weekdays.Wednesday, 17, 0, 105, "Štadión – hlavné ihrisko", TrainingType.Match,      "Presilovky, štandardné situácie, záverečný zápas."),
            ("A-tím",     "Regeneračná jednotka – A", Weekdays.Friday,    18, 0, 60, "Posilňovňa",             TrainingType.Recovery,     "Strečing, kompenzačné cvičenia, bazén podľa počasia."),
            ("B-tím",     "Technika a prihrávky – B", Weekdays.Tuesday,   18, 0, 90, "Hlavná telocvičňa",      TrainingType.Technical,    "Práca s loptou, krátke prihrávky, 1 na 1."),
            ("B-tím",     "Taktický tréning – B",     Weekdays.Thursday,  18, 0, 90, "Štadión – hlavné ihrisko", TrainingType.Technical,  "Rozostavenie, presun bloku, kombinácie po krídle."),
            ("Žiaci",     "Všeobecná príprava – Ž",   Weekdays.Tuesday,   16, 0, 90, "Hlavná telocvičňa",      TrainingType.Conditioning, "Koordinácia, rýchlosť a obratnosť formou hier."),
            ("Žiaci",     "Zápasová simulácia – Ž",   Weekdays.Thursday,  16, 0, 105, "Štadión – hlavné ihrisko", TrainingType.Match,     "Modelové herné situácie a rohové kopy."),
            ("Prípravka", "Pohybová príprava – P",    Weekdays.Monday,    16, 0, 60, "Vedľajšia telocvičňa",   TrainingType.Conditioning, "Základná pohybová abeceda, prekážkové dráhy."),
            ("Prípravka", "Hravý tréning – P",        Weekdays.Wednesday, 16, 0, 60, "Hlavná telocvičňa",      TrainingType.Technical,    "Práca s loptou formou hry, striedanie stanovíšť.")
        };

        var created = new List<TrainingSeries>();

        foreach (var t in templates)
        {
            if (!teamByName.TryGetValue(t.TeamName, out var team))
                continue;

            var coachId = coachByTeam.GetValueOrDefault(team.Id) ?? fallbackCoachId;
            if (coachId == null)
                continue;

            var start = new TimeOnly(t.Hour, t.Minute);

            created.Add(new TrainingSeries
            {
                Id         = Guid.NewGuid(),
                TeamId     = team.Id,
                CoachId    = coachId,
                SeasonId   = activeSeason.Id,
                Title      = t.Title,
                DaysOfWeek = (int)t.Days,
                StartTime  = start,
                EndTime    = start.AddMinutes(t.Minutes),
                Location   = t.Location,
                Type       = t.Type,
                CoachNote  = t.Note,
                IsActive   = true
            });
        }

        _context.TrainingSeries.AddRange(created);
        await _context.SaveChangesAsync();

        // BuildMissingInstances reads series.Season, and the tracked entities above were built from
        // raw ids, so hand it the season the rows were just created against.
        foreach (var series in created)
            series.Season = activeSeason;

        _logger.LogInformation("Naseedovaných {Count} opakovaných tréningov.", created.Count);
        return created;
    }

    private async Task EnsureRichSampleHistoryAsync(
        List<Team> teams,
        List<TrainingSeries> series,
        List<ApplicationUser> sampleChildren,
        ApplicationUser? markedByUser,
        ApplicationUser? parent,
        DateTime anchor)
    {
        if (!teams.Any() || !sampleChildren.Any())
            return;

        // Guard: ak existuje viac ako 3 tréningy, história už bola naseedovaná
        if (await _context.TrainingEvents.CountAsync() > 3)
            return;

        var activeSeason = await _context.Seasons.FirstOrDefaultAsync(s => s.IsActive);
        if (activeSeason == null)
            return;

        // Not a fixed seed anymore: a fixed Random would regenerate byte-identical
        // attendance/absence patterns every single refresh, which looks obviously canned on a
        // demo that's supposed to look freshly lived-in every day.
        var rng         = new Random();
        var now         = anchor;
        var startWindow = now.AddDays(-90);

        var trainings  = new List<TrainingEvent>();
        var attendances = new List<AttendanceRecord>();
        var excuses    = new List<AbsenceRequest>();

        var teamMemberCache = new Dictionary<Guid, List<string>>();
        foreach (var team in teams)
        {
            var members = await _context.TeamMembers
                .Where(tm => tm.TeamId == team.Id
                          && sampleChildren.Select(c => c.Id).Contains(tm.UserId))
                .Select(tm => tm.UserId)
                .ToListAsync();
            teamMemberCache[team.Id] = members;
        }

        // Expanded from the weekly plan through the same pure function the "Generate" button and the
        // nightly worker use, so the calendar a visitor sees is exactly what those series produce.
        // 90 days back gives the statistics something to chart; 21 days forward leaves enough
        // untouched trainings for a visitor to actually file an excuse against one.
        var from = DateOnly.FromDateTime(startWindow.Date);
        var to   = DateOnly.FromDateTime(now.Date.AddDays(21));

        foreach (var s in series)
            trainings.AddRange(TrainingSeriesInstanceGenerator.BuildMissingInstances(s, from, to, []));

        foreach (var training in trainings)
        {
            // The generator stamps CreatedAt with "now" because that is right for a training being
            // scheduled today. For back-dated demo history it would make a three-month-old training
            // look like it was created this morning.
            training.CreatedAt = training.StartTime.AddDays(-14);

            // Dochádzka len pre minulé tréningy
            if (training.StartTime > now)
                continue;

            var members = teamMemberCache.GetValueOrDefault(training.TeamId) ?? [];
            foreach (var childId in members)
            {
                // Distribúcia: 70 % Prítomný / 18 % Ospravedlnený / 12 % Neprítomný
                var roll   = rng.Next(100);
                var status = roll < 70 ? AttendanceStatus.Present
                           : roll < 88 ? AttendanceStatus.Excused
                           : AttendanceStatus.Absent;

                attendances.Add(new AttendanceRecord
                {
                    Id              = Guid.NewGuid(),
                    TrainingEventId = training.Id,
                    ChildId         = childId,
                    Status          = status,
                    Note            = status == AttendanceStatus.Absent ? "Neospravedlnená absencia" : null,
                    MarkedByUserId  = markedByUser?.Id,
                    RecordedAt      = training.StartTime.AddHours(2)
                });
            }
        }

        // Ospravedlnenky pre nadchádzajúce tréningy
        if (parent != null)
        {
            var futureTrainings = trainings
                .Where(t => t.StartTime > now)
                .OrderBy(t => t.StartTime)
                .Take(12)
                .ToList();

            var reasons = new[]
            {
                "Choroba – chrípka",
                "Rodinná dovolenka",
                "Návšteva u lekára",
                "Školský výlet",
                "Príprava na skúšku",
                "Iný šport – turnaj",
                "Rodinná oslava",
                "Doprava nedostupná"
            };

            foreach (var training in futureTrainings)
            {
                var members = teamMemberCache[training.TeamId];
                if (members.Count == 0) continue;

                var childId = members[rng.Next(members.Count)];
                excuses.Add(new AbsenceRequest
                {
                    Id              = Guid.NewGuid(),
                    ChildId         = childId,
                    ParentId        = parent.Id,
                    TrainingEventId = training.Id,
                    DateFrom        = training.StartTime,
                    DateTo          = training.EndTime,
                    Reason          = reasons[rng.Next(reasons.Length)],
                    Status          = AbsenceRequestStatus.Received,
                    CreatedAt       = now.AddDays(-rng.Next(1, 14))
                });
            }
        }

        _context.TrainingEvents.AddRange(trainings);
        await _context.SaveChangesAsync();

        _context.AttendanceRecords.AddRange(attendances);
        _context.AbsenceRequests.AddRange(excuses);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Naseedovaných {Trainings} tréningov, {Attendances} záznamov dochádzky, {Excuses} ospravedlnení.",
            trainings.Count, attendances.Count, excuses.Count);
    }

    private async Task EnsureSampleAnnouncementsAsync(ApplicationUser? author, List<Team> teams, DateTime anchor)
    {
        if (author == null)
            return;

        if (await _context.Announcements.AnyAsync())
            return;

        var teamByName = teams.ToDictionary(t => t.Name, t => t.Id);
        var aTeamId = teamByName.GetValueOrDefault("A-tím", teams[0].Id);
        var ziaciTeamId = teamByName.GetValueOrDefault("Žiaci", teams.Count > 2 ? teams[2].Id : teams[0].Id);

        // Dates below are computed relative to the seed anchor rather than hardcoded, so the
        // announcements still read as "this is happening soon" a year (or five) after this
        // code was written, instead of referencing a June 2026 that has long since passed.
        var campStart = anchor.AddDays(45);
        var campEnd = campStart.AddDays(7);
        var campDeadline = anchor.AddDays(18);
        var trainingChangeDate = anchor.AddDays(6);
        var parentsMeetingDate = anchor.AddDays(10);
        var jerseyPickupDate = anchor.AddDays(((8 - (int)anchor.DayOfWeek) % 7) + 7); // next Monday, at least a week out
        var semifinalDate = anchor.AddDays(14);

        _context.Announcements.AddRange(
            new Announcement
            {
                Id             = Guid.NewGuid(),
                Title          = $"Letný tréningový tábor – prihlásenie do {campDeadline:d. MMMM}",
                Content        = $"Vážení rodičia a športovci,\n\noznamujeme otvorenie prihlásenia na letný tréningový tábor ŠK ZISK, ktorý sa uskutoční od {campStart:d. MMMM} do {campEnd:d. MMMM} v Nízkych Tatrách.\n\nPrihlasovanie prebieha cez formulár v sekcii Dokumenty alebo osobne v kancelárii klubu každý pracovný deň od 15:00 do 18:00.\n\nKapacita je obmedzená.",
                TargetTeamId   = null,
                TargetAudience = TargetAudience.All,
                Priority       = AnnouncementPriority.High,
                IsPinned       = true,
                ValidUntil     = campDeadline,
                AuthorUserId   = author.Id,
                PublishDate    = anchor.AddDays(-2)
            },
            new Announcement
            {
                Id             = Guid.NewGuid(),
                Title          = $"Zmena termínu tréningu A-tímu – {trainingChangeDate:d. MMMM}",
                Content        = $"Upozorňujeme členov A-tímu, že tréning naplánovaný na {trainingChangeDate:d. MMMM} sa presúva z 17:00 na 18:30 z dôvodu rekonštrukcie telocvične.\n\nMiesto zostáva rovnaké – Hlavná telocvičňa.",
                TargetTeamId   = aTeamId,
                TargetAudience = TargetAudience.Athletes,
                Priority       = AnnouncementPriority.Medium,
                IsPinned       = false,
                ValidUntil     = trainingChangeDate.AddDays(1),
                AuthorUserId   = author.Id,
                PublishDate    = anchor.AddDays(-1)
            },
            new Announcement
            {
                Id             = Guid.NewGuid(),
                Title          = $"Stretnutie rodičov žiakov – {parentsMeetingDate:d. MMMM} o 18:00",
                Content        = $"Pozývame rodičov žiakov na informačné stretnutie, ktoré sa uskutoční {parentsMeetingDate:d. MMMM yyyy} o 18:00 v zasadacej miestnosti klubu.\n\nProgram:\n• Hodnotenie aktuálnej časti sezóny\n• Informácie o tábore a letnom sústredení\n• Rôzne\n\nÚčasť je vítaná.",
                TargetTeamId   = ziaciTeamId,
                TargetAudience = TargetAudience.Parents,
                Priority       = AnnouncementPriority.High,
                IsPinned       = false,
                ValidUntil     = parentsMeetingDate,
                AuthorUserId   = author.Id,
                PublishDate    = anchor.AddHours(-18)
            },
            new Announcement
            {
                Id             = Guid.NewGuid(),
                Title          = $"Nové dresy – vyzdvihnutie v pondelok {jerseyPickupDate:d. MMMM} od 16:00",
                Content        = $"Informujeme všetkých hráčov, že nové klubové dresy sú k dispozícii na vyzdvihnutie od pondelka {jerseyPickupDate:d. MMMM yyyy} v čase 16:00 – 19:00 pri vstupe do telocvične.\n\nPrineste so sebou potvrdenie o zaplatení členského príspevku.",
                TargetTeamId   = null,
                TargetAudience = TargetAudience.Athletes,
                Priority       = AnnouncementPriority.Medium,
                IsPinned       = false,
                ValidUntil     = jerseyPickupDate.AddDays(3),
                AuthorUserId   = author.Id,
                PublishDate    = anchor.AddHours(-6)
            },
            new Announcement
            {
                Id             = Guid.NewGuid(),
                Title          = "Výsledky posledného kola – A-tím postupuje do semifinále!",
                Content        = $"S radosťou oznamujeme, že A-tím ŠK ZISK postúpil do semifinále aktuálneho súťažného kola III. ligy po víťazstve 3:1 nad FK Záhorie.\n\nGratulujeme celému tímu a trénerovi Markovi Kováčikovi! Semifinálový zápas sa uskutoční {semifinalDate:d. MMMM} na domácom štadióne.\n\nTešíme sa na vašu podporu!",
                TargetTeamId   = aTeamId,
                TargetAudience = TargetAudience.All,
                Priority       = AnnouncementPriority.Low,
                IsPinned       = false,
                ValidUntil     = semifinalDate.AddDays(2),
                AuthorUserId   = author.Id,
                PublishDate    = anchor.AddHours(-48)
            }
        );

        await _context.SaveChangesAsync();
    }

    private async Task EnsureSampleDocumentsAsync(ApplicationUser? uploader, DateTime anchor)
    {
        if (await _context.Documents.AnyAsync())
            return;

        _context.Documents.AddRange(
            new Document
            {
                Id               = Guid.NewGuid(),
                Title            = "Stanovy ŠK ZISK",
                FilePath         = "/uploads/documents/stanovy-sk-zisk.pdf",
                Category         = DocumentCategory.General,
                TargetRoleId     = null,
                UploadedByUserId = uploader?.Id,
                UploadedAt       = anchor.AddDays(-30)
            },
            new Document
            {
                Id               = Guid.NewGuid(),
                Title            = "Súhlas zákonného zástupcu – spracovanie osobných údajov",
                FilePath         = "/uploads/documents/suhlas-gdpr.pdf",
                Category         = DocumentCategory.Contract,
                TargetRoleId     = null,
                UploadedByUserId = uploader?.Id,
                UploadedAt       = anchor.AddDays(-14)
            },
            new Document
            {
                Id               = Guid.NewGuid(),
                Title            = "Tréningový plán – aktuálna sezóna",
                FilePath         = "/uploads/documents/treningovy-plan.pdf",
                Category         = DocumentCategory.TrainingPlan,
                TargetRoleId     = null,
                UploadedByUserId = uploader?.Id,
                UploadedAt       = anchor.AddDays(-7)
            },
            new Document
            {
                Id               = Guid.NewGuid(),
                Title            = "Zdravotná karta športovca",
                FilePath         = "/uploads/documents/zdravotna-karta-sportovca.docx",
                Category         = DocumentCategory.Contract,
                TargetRoleId     = null,
                UploadedByUserId = uploader?.Id,
                UploadedAt       = anchor.AddDays(-3)
            }
        );

        await _context.SaveChangesAsync();
    }
}
