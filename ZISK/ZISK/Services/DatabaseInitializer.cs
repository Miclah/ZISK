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

    public DatabaseInitializer(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ILogger<DatabaseInitializer> logger,
        IConfiguration configuration,
        IOptions<SeedPasswordOptions> passwordOptions,
        IOptions<SeedInitialAdminOptions> initialAdminOptions)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
        _configuration = configuration;
        _passwordOptions = passwordOptions.Value;
        _initialAdminOptions = initialAdminOptions.Value;
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

        return new SeedPasswordSet(_passwordOptions.Admin!, _passwordOptions.Coach!, _passwordOptions.Parent!, _passwordOptions.Child!);
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

        var admin  = await EnsureUserAsync("admin@zisk.sk",  passwords.Admin,  "Admin",  "Miroslav", "Kráľ");
        var coach  = await EnsureUserAsync("trener@zisk.sk", passwords.Coach, "Coach",  "Marek",    "Kováčik");
        var parent = await EnsureUserAsync("rodic@zisk.sk",  passwords.Parent,  "Parent", "Peter",    "Novák");
        var child  = await EnsureUserAsync("dieta@zisk.sk",  passwords.Child,  "Child",  "Tomáš",    "Novák");

        if (child != null && child.DateOfBirth == null)
        {
            child.DateOfBirth = new DateOnly(2014, 5, 12);
            await _userManager.UpdateAsync(child);
        }

        await SeedTeamsAsync();
        await EnsureDefaultSeasonAsync();
        await EnsureChildSeedUserAsync(child, parent);
        await EnsureCoachTeamAssignmentsAsync(coach);
        await SeedSampleDataAsync(admin, coach, parent, child, passwords);
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

    private async Task EnsureDefaultSeasonAsync()
    {
        if (await _context.Seasons.AnyAsync())
            return;

        _context.Seasons.Add(new Season
        {
            Id        = Guid.NewGuid(),
            Name      = "Jar 2026",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate   = new DateOnly(2026, 6, 30),
            IsActive  = true,
            CreatedAt = DateTime.UtcNow
        });
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

            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                _logger.LogWarning("Unable to create seed user {Email}: {Error}",
                    email, string.Join(", ", result.Errors.Select(e => e.Description)));
                return await _userManager.FindByEmailAsync(email);
            }
        }

        if (!await _userManager.IsInRoleAsync(user, role))
            await _userManager.AddToRoleAsync(user, role);

        return user;
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
        SeedPasswordSet passwords)
    {
        var teams = await _context.Teams
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync();

        if (!teams.Any())
            return;

        var sampleUsers   = await EnsureSampleUsersAsync(passwords);
        var sampleChildren = await EnsureSampleChildrenAsync(teams, sampleUsers);

        // Zahrnúť aj hlavného testovacie dieťa do zoznamu pre dochádzku
        if (coreChild != null && !sampleChildren.Any(c => c.Id == coreChild.Id))
            sampleChildren.Insert(0, coreChild);

        await EnsureSampleParentLinksAsync(defaultParent, sampleUsers, coreChild);
        await EnsureSampleCoachAssignmentsAsync(sampleUsers, teams);

        var sampleTrainings = await EnsureSampleTrainingsAsync(teams);
        var attendanceUser  = defaultCoach ?? sampleUsers.Coaches.FirstOrDefault() ?? admin;

        await EnsureSampleAttendanceAsync(sampleTrainings, sampleChildren, attendanceUser);

        var sampleParent = sampleUsers.Parents.FirstOrDefault() ?? defaultParent;
        await EnsureSampleAbsenceRequestsAsync(sampleTrainings, sampleChildren, sampleParent);

        await EnsureRichSampleHistoryAsync(teams, sampleChildren, attendanceUser, sampleParent);

        var announcementAuthor = admin ?? defaultCoach ?? sampleUsers.Coaches.FirstOrDefault();
        await EnsureSampleAnnouncementsAsync(announcementAuthor, teams);
        await EnsureSampleDocumentsAsync(admin);
    }

    private async Task<SampleUserGroup> EnsureSampleUsersAsync(SeedPasswordSet passwords)
    {
        var coaches  = new List<ApplicationUser>();
        var parents  = new List<ApplicationUser>();
        var athletes = new List<ApplicationUser>();
        var children = new List<ApplicationUser>();

        // Tréneri
        foreach (var (email, fn, ln) in new[]
        {
            ("rastislav.horvath@zisk.sk", "Rastislav", "Horváth"),
            ("tomas.balaz@zisk.sk",       "Tomáš",     "Baláž"),
            ("jan.minac@zisk.sk",         "Ján",       "Mináč")
        })
        {
            var u = await EnsureUserAsync(email, passwords.Coach, "Coach", fn, ln);
            if (u != null) coaches.Add(u);
        }

        // Rodičia
        foreach (var (email, fn, ln) in new[]
        {
            ("jana.novakova@zisk.sk",   "Jana",    "Nováková"),
            ("milan.horak@zisk.sk",     "Milan",   "Horák"),
            ("andrea.blahova@zisk.sk",  "Andrea",  "Bláhová"),
            ("lukas.kral@zisk.sk",      "Lukáš",   "Kráľ"),
            ("monika.simonova@zisk.sk", "Monika",  "Šimonová"),
            ("juraj.balog@zisk.sk",     "Juraj",   "Balog"),
            ("zuzana.oravec@zisk.sk",   "Zuzana",  "Oravec")
        })
        {
            var u = await EnsureUserAsync(email, passwords.Parent, "Parent", fn, ln);
            if (u != null) parents.Add(u);
        }

        // Starší športovci (Athlete) — A-tím a B-tím
        foreach (var (email, fn, ln, dob) in new[]
        {
            ("lukas.maly@zisk.sk",      "Lukáš",   "Malý",   new DateOnly(2004, 3, 15)),
            ("martin.horak@zisk.sk",    "Martin",  "Horák",  new DateOnly(2005, 7, 22)),
            ("jakub.blaha@zisk.sk",     "Jakub",   "Bláha",  new DateOnly(2004, 11, 8)),
            ("adam.kral@zisk.sk",       "Adam",    "Kráľ",   new DateOnly(2005, 2, 14)),
            ("michal.simon@zisk.sk",    "Michal",  "Šimon",  new DateOnly(2007, 5, 3)),
            ("juraj.balog.jr@zisk.sk",  "Juraj",   "Balog",  new DateOnly(2006, 9, 18)),
            ("richard.varga@zisk.sk",   "Richard", "Varga",  new DateOnly(2007, 1, 25))
        })
        {
            var u = await EnsureUserAsync(email, passwords.Child, "Athlete", fn, ln, dob);
            if (u != null) athletes.Add(u);
        }

        // Mladší deti (Child) — Žiaci a Prípravka
        foreach (var (email, fn, ln, dob) in new[]
        {
            ("petra.horakova@zisk.sk",  "Petra",   "Horáková", new DateOnly(2010, 4, 7)),
            ("klara.oravec@zisk.sk",    "Klára",   "Oravec",   new DateOnly(2011, 6, 12)),
            ("filip.cerny@zisk.sk",     "Filip",   "Čierny",   new DateOnly(2010, 9, 30)),
            ("zuzana.kralova@zisk.sk",  "Zuzana",  "Kráľová",  new DateOnly(2011, 3, 17)),
            ("samuel.novak@zisk.sk",    "Samuel",  "Novák",    new DateOnly(2014, 8, 20)),
            ("ema.holubova@zisk.sk",    "Ema",     "Holúbová", new DateOnly(2015, 1, 9)),
            ("ondrej.maly@zisk.sk",     "Ondrej",  "Malý",     new DateOnly(2014, 11, 3)),
            ("nina.blahova@zisk.sk",    "Nina",    "Bláhová",  new DateOnly(2015, 5, 28))
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

        var links = new List<(string ParentEmail, string ChildEmail, bool IsPrimary)>
        {
            // Jana Nováková je sekundárna mama Tomáša (primárny otec Peter Novák — linknutý cez EnsureChildSeedUserAsync)
            ("jana.novakova@zisk.sk", "dieta@zisk.sk",         false),
            // Peter Novák (rodic@zisk.sk) — aj Samuel
            ("rodic@zisk.sk",         "samuel.novak@zisk.sk",  true),
            // Milan Horák — Martin a Petra
            ("milan.horak@zisk.sk",   "martin.horak@zisk.sk",  true),
            ("milan.horak@zisk.sk",   "petra.horakova@zisk.sk",true),
            // Andrea Bláhová — Jakub a Nina
            ("andrea.blahova@zisk.sk","jakub.blaha@zisk.sk",   true),
            ("andrea.blahova@zisk.sk","nina.blahova@zisk.sk",  true),
            // Lukáš Kráľ — Adam a Zuzana
            ("lukas.kral@zisk.sk",    "adam.kral@zisk.sk",     true),
            ("lukas.kral@zisk.sk",    "zuzana.kralova@zisk.sk",true),
            // Monika Šimonová — Michal
            ("monika.simonova@zisk.sk","michal.simon@zisk.sk", true),
            // Juraj Balog — Juraj Jr.
            ("juraj.balog@zisk.sk",   "juraj.balog.jr@zisk.sk",true),
            // Zuzana Oravec — Klára
            ("zuzana.oravec@zisk.sk", "klara.oravec@zisk.sk",  true)
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

    private async Task<List<TrainingEvent>> EnsureSampleTrainingsAsync(List<Team> teams)
    {
        // Ak už existujú tréningy, preskočíme (rich history ich vytvorí neskôr)
        if (await _context.TrainingEvents.AnyAsync())
            return [];

        var activeSeason = await _context.Seasons.FirstOrDefaultAsync(s => s.IsActive);
        if (activeSeason == null)
            return [];

        var teamByName = teams.ToDictionary(t => t.Name, t => t);
        var now = DateTime.UtcNow;

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

    private async Task EnsureRichSampleHistoryAsync(
        List<Team> teams,
        List<ApplicationUser> sampleChildren,
        ApplicationUser? markedByUser,
        ApplicationUser? parent)
    {
        if (!teams.Any() || !sampleChildren.Any())
            return;

        // Guard: ak existuje viac ako 3 tréningy, história už bola naseedovaná
        if (await _context.TrainingEvents.CountAsync() > 3)
            return;

        var activeSeason = await _context.Seasons.FirstOrDefaultAsync(s => s.IsActive);
        if (activeSeason == null)
            return;

        var rng         = new Random(42); // fixný seed — rovnaké dáta pri každom reštarte
        var now         = DateTime.UtcNow;
        var startWindow = now.AddDays(-90);

        var trainings  = new List<TrainingEvent>();
        var attendances = new List<AttendanceRecord>();
        var excuses    = new List<AbsenceRequest>();

        var typeRotation = new[]
        {
            TrainingType.Conditioning,
            TrainingType.Technical,
            TrainingType.Match,
            TrainingType.Recovery,
            TrainingType.Conditioning,
            TrainingType.Technical
        };

        var locations = new[] { "Hlavná telocvičňa", "Vedľajšia telocvičňa", "Štadión", "Posilňovňa" };
        var titles    = new[]
        {
            "Kondičný tréning",
            "Technika a prihrávky",
            "Taktický tréning",
            "Zápasová simulácia",
            "Regeneračná jednotka",
            "Rýchlosť a obratnosť",
            "Herné situácie"
        };

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

        // Generujeme tréningy za posledných 90 dní + 7 dní dopredu
        for (var day = startWindow.Date; day <= now.Date.AddDays(7); day = day.AddDays(1))
        {
            for (var teamIndex = 0; teamIndex < teams.Count; teamIndex++)
            {
                var team      = teams[teamIndex];
                var dayOfWeek = (int)day.DayOfWeek;
                // Párne tímy: pondelok + streda (1, 3); nepárne: utorok + štvrtok (2, 4)
                var trainingDays = teamIndex % 2 == 0 ? new[] { 1, 3 } : new[] { 2, 4 };
                if (!trainingDays.Contains(dayOfWeek))
                    continue;

                var hour      = 16 + (teamIndex % 3);
                var startTime = day.AddHours(hour);
                var type      = typeRotation[(teamIndex + day.DayOfYear) % typeRotation.Length];
                var titleBase = titles[(teamIndex + day.DayOfYear) % titles.Length];
                var title     = $"{titleBase} – {team.ShortName}";

                var training = new TrainingEvent
                {
                    Id        = Guid.NewGuid(),
                    TeamId    = team.Id,
                    SeasonId  = activeSeason.Id,
                    Title     = title,
                    StartTime = startTime,
                    EndTime   = startTime.AddMinutes(90),
                    Location  = locations[(teamIndex + day.DayOfYear) % locations.Length],
                    Type      = type,
                    CoachNote = null,
                    CreatedAt = startTime.AddDays(-7),
                    IsLocked  = false
                };
                trainings.Add(training);

                // Dochádzka len pre minulé tréningy
                if (training.StartTime > now)
                    continue;

                var members = teamMemberCache[team.Id];
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

    private async Task EnsureSampleAnnouncementsAsync(ApplicationUser? author, List<Team> teams)
    {
        if (author == null)
            return;

        if (await _context.Announcements.AnyAsync())
            return;

        var teamByName = teams.ToDictionary(t => t.Name, t => t.Id);
        var aTeamId = teamByName.GetValueOrDefault("A-tím", teams[0].Id);
        var ziaciTeamId = teamByName.GetValueOrDefault("Žiaci", teams.Count > 2 ? teams[2].Id : teams[0].Id);

        _context.Announcements.AddRange(
            new Announcement
            {
                Id             = Guid.NewGuid(),
                Title          = "Letný tréningový tábor 2026 – prihlásenie do 20. júna",
                Content        = "Vážení rodičia a športovci,\n\noznamujeme otvorenie prihlásenia na letný tréningový tábor ŠK ZISK, ktorý sa uskutoční od 7. do 14. júla 2026 v Nízkych Tatrách.\n\nPrihlasovanie prebieha cez formulár v sekcii Dokumenty alebo osobne v kancelárii klubu každý pracovný deň od 15:00 do 18:00.\n\nKapacita je obmedzená.",
                TargetTeamId   = null,
                TargetAudience = TargetAudience.All,
                Priority       = AnnouncementPriority.High,
                IsPinned       = true,
                ValidUntil     = DateTime.UtcNow.AddDays(30),
                AuthorUserId   = author.Id,
                PublishDate    = DateTime.UtcNow.AddDays(-2)
            },
            new Announcement
            {
                Id             = Guid.NewGuid(),
                Title          = "Zmena termínu tréningu A-tímu – 18. júna",
                Content        = "Upozorňujeme členov A-tímu, že tréning naplánovaný na 18. júna (streda) sa presúva z 17:00 na 18:30 z dôvodu rekonštrukcie telocvične.\n\nMiesto zostáva rovnaké – Hlavná telocvičňa.",
                TargetTeamId   = aTeamId,
                TargetAudience = TargetAudience.Athletes,
                Priority       = AnnouncementPriority.Medium,
                IsPinned       = false,
                ValidUntil     = DateTime.UtcNow.AddDays(14),
                AuthorUserId   = author.Id,
                PublishDate    = DateTime.UtcNow.AddDays(-1)
            },
            new Announcement
            {
                Id             = Guid.NewGuid(),
                Title          = "Stretnutie rodičov žiakov – 12. júna o 18:00",
                Content        = "Pozývame rodičov žiakov na informačné stretnutie, ktoré sa uskutoční 12. júna 2026 o 18:00 v zasadacej miestnosti klubu.\n\nProgram:\n• Hodnotenie jarnej časti sezóny\n• Informácie o tábore a letnom sústredení\n• Rôzne\n\nÚčasť je vítaná.",
                TargetTeamId   = ziaciTeamId,
                TargetAudience = TargetAudience.Parents,
                Priority       = AnnouncementPriority.High,
                IsPinned       = false,
                ValidUntil     = DateTime.UtcNow.AddDays(10),
                AuthorUserId   = author.Id,
                PublishDate    = DateTime.UtcNow.AddHours(-18)
            },
            new Announcement
            {
                Id             = Guid.NewGuid(),
                Title          = "Nové dresy – vyzdvihnutie v pondelok od 16:00",
                Content        = "Informujeme všetkých hráčov, že nové klubové dresy sú k dispozícii na vyzdvihnutie od pondelka 9. júna 2026 v čase 16:00 – 19:00 pri vstupe do telocvične.\n\nPrineste so sebou potvrdenie o zaplatení členského príspevku.",
                TargetTeamId   = null,
                TargetAudience = TargetAudience.Athletes,
                Priority       = AnnouncementPriority.Medium,
                IsPinned       = false,
                ValidUntil     = DateTime.UtcNow.AddDays(7),
                AuthorUserId   = author.Id,
                PublishDate    = DateTime.UtcNow.AddHours(-6)
            },
            new Announcement
            {
                Id             = Guid.NewGuid(),
                Title          = "Výsledky jarného kola – A-tím postupuje do semifinále!",
                Content        = "S radosťou oznamujeme, že A-tím ŠK ZISK postúpil do semifinále jarného kola III. ligy po víťazstve 3:1 nad FK Záhorie.\n\nGratulujeme celému tímu a trénerovi Markovi Kováčikovi! Semifinálový zápas sa uskutoční 28. júna 2026 na domácom štadióne.\n\nTešíme sa na vašu podporu!",
                TargetTeamId   = aTeamId,
                TargetAudience = TargetAudience.All,
                Priority       = AnnouncementPriority.Low,
                IsPinned       = false,
                ValidUntil     = DateTime.UtcNow.AddDays(60),
                AuthorUserId   = author.Id,
                PublishDate    = DateTime.UtcNow.AddHours(-48)
            }
        );

        await _context.SaveChangesAsync();
    }

    private async Task EnsureSampleDocumentsAsync(ApplicationUser? uploader)
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
                UploadedAt       = DateTime.UtcNow.AddDays(-30)
            },
            new Document
            {
                Id               = Guid.NewGuid(),
                Title            = "Súhlas zákonného zástupcu – spracovanie osobných údajov",
                FilePath         = "/uploads/documents/suhlas-gdpr.pdf",
                Category         = DocumentCategory.Contract,
                TargetRoleId     = null,
                UploadedByUserId = uploader?.Id,
                UploadedAt       = DateTime.UtcNow.AddDays(-14)
            },
            new Document
            {
                Id               = Guid.NewGuid(),
                Title            = "Tréningový plán – jar 2026",
                FilePath         = "/uploads/documents/treningovy-plan-jar-2026.pdf",
                Category         = DocumentCategory.TrainingPlan,
                TargetRoleId     = null,
                UploadedByUserId = uploader?.Id,
                UploadedAt       = DateTime.UtcNow.AddDays(-7)
            },
            new Document
            {
                Id               = Guid.NewGuid(),
                Title            = "Zdravotná karta športovca",
                FilePath         = "/uploads/documents/zdravotna-karta-sportovca.docx",
                Category         = DocumentCategory.Contract,
                TargetRoleId     = null,
                UploadedByUserId = uploader?.Id,
                UploadedAt       = DateTime.UtcNow.AddDays(-3)
            }
        );

        await _context.SaveChangesAsync();
    }
}
