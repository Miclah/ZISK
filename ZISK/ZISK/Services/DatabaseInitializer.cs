using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ZISK.Data;
using ZISK.Data.Entities;

namespace ZISK.Services;

public class DatabaseInitializer
{
    private const string SamplePrefix = "[SAMPLE]";

    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ILogger<DatabaseInitializer> logger)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        await _context.Database.MigrateAsync();

        await SeedRolesAsync();

        var admin = await EnsureUserAsync("admin@zisk.sk", "Admin1234", "Admin", "Admin", "ZISK");
        var coach = await EnsureUserAsync("trener@zisk.sk", "Trener1234", "Coach", "Ján", "Tréner");
        var parent = await EnsureUserAsync("rodic@zisk.sk", "Rodic1234", "Parent", "Peter", "Rodič");
        var child = await EnsureUserAsync("dieta@zisk.sk", "Dieta1234", "Child", "Tomáš", "Dieťa");

        await SeedTeamsAsync();
        await EnsureDefaultSeasonAsync();
        await EnsureChildSeedUserAsync(child, parent);
        await EnsureCoachTeamAssignmentsAsync(coach);
        await SeedSampleDataAsync(admin, coach, parent);
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

        var season = new Season
        {
            Id = Guid.NewGuid(),
            Name = "Jar 2026",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 6, 30),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
    }

    private async Task EnsureChildSeedUserAsync(ApplicationUser? childUser, ApplicationUser? parent)
    {
        if (childUser == null)
            return;

        // Ensure child has a team membership
        var hasTeam = await _context.TeamMembers.AnyAsync(tm => tm.UserId == childUser.Id);
        if (!hasTeam)
        {
            var teamId = await _context.Teams
                .Where(t => t.IsActive)
                .Select(t => (Guid?)t.Id)
                .FirstOrDefaultAsync();

            if (teamId.HasValue)
            {
                _context.TeamMembers.Add(new TeamMember
                {
                    TeamId = teamId.Value,
                    UserId = childUser.Id,
                    JoinedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }
        }

        // Ensure parent-child link
        if (parent != null)
        {
            var hasLink = await _context.ParentChildren
                .AnyAsync(pc => pc.ParentId == parent.Id && pc.ChildId == childUser.Id);

            if (!hasLink)
            {
                _context.ParentChildren.Add(new ParentChild
                {
                    ParentId = parent.Id,
                    ChildId = childUser.Id,
                    IsPrimary = true
                });
                await _context.SaveChangesAsync();
            }
        }
    }

    private async Task<ApplicationUser?> EnsureUserAsync(string email, string password, string role, string firstName, string lastName)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                EmailConfirmed = true,
                IsActive = true
            };

            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                _logger.LogWarning("Unable to create seed user {Email}: {Error}", email, string.Join(", ", result.Errors.Select(e => e.Description)));
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

        var teams = new List<Team>
        {
            new() { Id = Guid.NewGuid(), Name = "A-tím", ShortName = "A", Description = "Hlavný seniorský tím", IsActive = true },
            new() { Id = Guid.NewGuid(), Name = "B-tím", ShortName = "B", Description = "Záložný seniorský tím", IsActive = true },
            new() { Id = Guid.NewGuid(), Name = "Žiaci", ShortName = "Ž", Description = "Mládežnícky tím", IsActive = true },
            new() { Id = Guid.NewGuid(), Name = "Prípravka", ShortName = "P", Description = "Najmenší športovci", IsActive = true }
        };

        _context.Teams.AddRange(teams);
        await _context.SaveChangesAsync();
    }

    private async Task EnsureCoachTeamAssignmentsAsync(ApplicationUser? coach)
    {
        if (coach == null)
            return;

        var activeTeamIds = await _context.Teams
            .Where(t => t.IsActive)
            .Select(t => t.Id)
            .ToListAsync();

        if (!activeTeamIds.Any())
            return;

        var assignedTeamIds = await _context.CoachTeams
            .Where(ct => ct.CoachId == coach.Id)
            .Select(ct => ct.TeamId)
            .ToListAsync();

        var hasPrimary = await _context.CoachTeams
            .AnyAsync(ct => ct.CoachId == coach.Id && ct.IsPrimary);

        var firstNew = true;
        foreach (var teamId in activeTeamIds.Where(teamId => !assignedTeamIds.Contains(teamId)))
        {
            _context.CoachTeams.Add(new CoachTeam
            {
                Id = Guid.NewGuid(),
                CoachId = coach.Id,
                TeamId = teamId,
                IsPrimary = !hasPrimary && firstNew,
                AssignedAt = DateTime.UtcNow
            });
            firstNew = false;
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedSampleDataAsync(ApplicationUser? admin, ApplicationUser? defaultCoach, ApplicationUser? defaultParent)
    {
        var teams = await _context.Teams
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync();

        if (!teams.Any())
            return;

        var sampleUsers = await EnsureSampleUsersAsync();
        var sampleChildren = await EnsureSampleChildrenAsync(teams, sampleUsers);

        await EnsureSampleParentLinksAsync(defaultParent, sampleUsers, sampleChildren);
        await EnsureSampleCoachAssignmentsAsync(defaultCoach, sampleUsers, teams);

        var sampleTrainings = await EnsureSampleTrainingsAsync(teams);
        var attendanceUser = defaultCoach ?? sampleUsers.Coaches.FirstOrDefault() ?? admin;

        await EnsureSampleAttendanceAsync(sampleTrainings, sampleChildren, attendanceUser);

        var sampleParent = sampleUsers.Parents.FirstOrDefault() ?? defaultParent;
        await EnsureSampleAbsenceRequestsAsync(sampleTrainings, sampleChildren, sampleParent);

        // Bohata vzorka pre grafy a statistiky - generuje treningy/dochadzku/ospravedlnenky za posledne 3 mesiace
        await EnsureRichSampleHistoryAsync(teams, sampleChildren, attendanceUser, sampleParent);

        var announcementAuthor = admin ?? defaultCoach ?? sampleUsers.Coaches.FirstOrDefault();
        await EnsureSampleAnnouncementsAsync(announcementAuthor, teams);
        await EnsureSampleDocumentsAsync();
    }

    private async Task<SampleUserGroup> EnsureSampleUsersAsync()
    {
        var coaches = new List<ApplicationUser>();
        var parents = new List<ApplicationUser>();
        var athletes = new List<ApplicationUser>();
        var children = new List<ApplicationUser>();

        var coach1 = await EnsureUserAsync("coach.marek.sample@zisk.sk", "Sample1234", "Coach", "Marek", "SampleCoach");
        var coach2 = await EnsureUserAsync("coach.lukas.sample@zisk.sk", "Sample1234", "Coach", "Lukáš", "SampleCoach");
        var parent1 = await EnsureUserAsync("parent.jana.sample@zisk.sk", "Sample1234", "Parent", "Jana", "SampleParent");
        var parent2 = await EnsureUserAsync("parent.milan.sample@zisk.sk", "Sample1234", "Parent", "Milan", "SampleParent");
        var athlete1 = await EnsureUserAsync("athlete.adam.sample@zisk.sk", "Sample1234", "Athlete", "Adam", "SampleAthlete");
        var child1 = await EnsureUserAsync("child.nina.sample@zisk.sk", "Sample1234", "Child", "Nina", "SampleChild");

        if (coach1 != null) coaches.Add(coach1);
        if (coach2 != null) coaches.Add(coach2);
        if (parent1 != null) parents.Add(parent1);
        if (parent2 != null) parents.Add(parent2);
        if (athlete1 != null) athletes.Add(athlete1);
        if (child1 != null) children.Add(child1);

        return new SampleUserGroup(coaches, parents, athletes, children);
    }

    private async Task<List<ApplicationUser>> EnsureSampleChildrenAsync(List<Team> teams, SampleUserGroup sampleUsers)
    {
        var result = new List<ApplicationUser>();

        var athleteTeamId = teams.First().Id;
        var childTeamId = teams.Count > 1 ? teams[1].Id : teams.First().Id;
        var thirdTeamId = teams.Count > 2 ? teams[2].Id : teams.First().Id;

        var childDefinitions = new[]
        {
            new { User = sampleUsers.Athletes.FirstOrDefault(), TeamId = athleteTeamId,
                  FallbackEmail = "sample.adam.child@zisk.sk", FirstName = "Adam", LastName = "SampleAthlete",
                  DateOfBirth = new DateOnly(2011, 4, 10) },
            new { User = sampleUsers.Children.FirstOrDefault(), TeamId = childTeamId,
                  FallbackEmail = "sample.nina.child@zisk.sk", FirstName = "Nina", LastName = "SampleChild",
                  DateOfBirth = new DateOnly(2012, 9, 3) }
        };

        foreach (var def in childDefinitions)
        {
            var user = def.User;
            if (user == null)
                continue;

            // Ensure team membership
            var hasMembership = await _context.TeamMembers.AnyAsync(tm => tm.UserId == user.Id && tm.TeamId == def.TeamId);
            if (!hasMembership)
            {
                _context.TeamMembers.Add(new TeamMember
                {
                    TeamId = def.TeamId,
                    UserId = user.Id,
                    JoinedAt = DateTime.UtcNow
                });
            }

            result.Add(user);
        }

        // Create two pure-child sample users with no app login
        var extraChildren = new[]
        {
            new { Email = "ema.sampledata@zisk.sk", FirstName = "Ema", LastName = "SampleData",
                  DateOfBirth = new DateOnly(2013, 2, 17), TeamId = thirdTeamId },
            new { Email = "filip.sampledata@zisk.sk", FirstName = "Filip", LastName = "SampleData",
                  DateOfBirth = new DateOnly(2010, 12, 5), TeamId = athleteTeamId }
        };

        foreach (var def in extraChildren)
        {
            var user = await EnsureUserAsync(def.Email, "Sample1234", "Child", def.FirstName, def.LastName);
            if (user != null)
            {
                if (user.DateOfBirth == null)
                {
                    user.DateOfBirth = def.DateOfBirth;
                    await _userManager.UpdateAsync(user);
                }

                var hasMembership = await _context.TeamMembers.AnyAsync(tm => tm.UserId == user.Id && tm.TeamId == def.TeamId);
                if (!hasMembership)
                {
                    _context.TeamMembers.Add(new TeamMember
                    {
                        TeamId = def.TeamId,
                        UserId = user.Id,
                        JoinedAt = DateTime.UtcNow
                    });
                }
                result.Add(user);
            }
        }

        await _context.SaveChangesAsync();
        return result;
    }

    private async Task EnsureSampleParentLinksAsync(ApplicationUser? defaultParent, SampleUserGroup sampleUsers, List<ApplicationUser> sampleChildren)
    {
        var parentLinks = new List<(string ParentId, string ChildId, bool IsPrimary)>();

        if (defaultParent != null && sampleChildren.Any())
            parentLinks.Add((defaultParent.Id, sampleChildren[0].Id, true));

        var sampleParent1 = sampleUsers.Parents.ElementAtOrDefault(0);
        var sampleParent2 = sampleUsers.Parents.ElementAtOrDefault(1);

        if (sampleParent1 != null && sampleChildren.Count > 1)
            parentLinks.Add((sampleParent1.Id, sampleChildren[1].Id, true));

        if (sampleParent2 != null)
        {
            if (sampleChildren.Count > 2)
                parentLinks.Add((sampleParent2.Id, sampleChildren[2].Id, true));
            if (sampleChildren.Count > 3)
                parentLinks.Add((sampleParent2.Id, sampleChildren[3].Id, false));
        }

        foreach (var link in parentLinks)
        {
            var exists = await _context.ParentChildren
                .AnyAsync(pc => pc.ParentId == link.ParentId && pc.ChildId == link.ChildId);

            if (!exists)
            {
                _context.ParentChildren.Add(new ParentChild
                {
                    ParentId = link.ParentId,
                    ChildId = link.ChildId,
                    IsPrimary = link.IsPrimary
                });
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task EnsureSampleCoachAssignmentsAsync(ApplicationUser? defaultCoach, SampleUserGroup sampleUsers, List<Team> teams)
    {
        var assignments = new List<(ApplicationUser Coach, Guid TeamId, bool IsPrimary)>();

        if (defaultCoach != null)
            assignments.Add((defaultCoach, teams.First().Id, true));

        var sampleCoach1 = sampleUsers.Coaches.ElementAtOrDefault(0);
        var sampleCoach2 = sampleUsers.Coaches.ElementAtOrDefault(1);

        if (sampleCoach1 != null)
        {
            assignments.Add((sampleCoach1, teams.First().Id, true));
            if (teams.Count > 1)
                assignments.Add((sampleCoach1, teams[1].Id, false));
        }

        if (sampleCoach2 != null)
        {
            var primaryTeamId = teams.Count > 2 ? teams[2].Id : teams.First().Id;
            assignments.Add((sampleCoach2, primaryTeamId, true));
        }

        foreach (var assignment in assignments)
        {
            var exists = await _context.CoachTeams
                .AnyAsync(ct => ct.CoachId == assignment.Coach.Id && ct.TeamId == assignment.TeamId);

            if (exists) continue;

            var hasPrimary = await _context.CoachTeams
                .AnyAsync(ct => ct.CoachId == assignment.Coach.Id && ct.IsPrimary);

            _context.CoachTeams.Add(new CoachTeam
            {
                Id = Guid.NewGuid(),
                CoachId = assignment.Coach.Id,
                TeamId = assignment.TeamId,
                IsPrimary = assignment.IsPrimary && !hasPrimary,
                AssignedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
    }

    private async Task<List<TrainingEvent>> EnsureSampleTrainingsAsync(List<Team> teams)
    {
        var existingSampleTrainings = await _context.TrainingEvents
            .Where(t => t.Title.StartsWith(SamplePrefix))
            .ToListAsync();

        if (existingSampleTrainings.Any())
            return existingSampleTrainings;

        var activeSeason = await _context.Seasons.FirstOrDefaultAsync(s => s.IsActive);
        if (activeSeason == null)
            return [];

        var now = DateTime.UtcNow;
        var trainings = new List<TrainingEvent>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TeamId = teams.First().Id,
                SeasonId = activeSeason.Id,
                Title = $"{SamplePrefix} Kondičný tréning A-tím",
                StartTime = now.AddDays(-3).Date.AddHours(17),
                EndTime = now.AddDays(-3).Date.AddHours(18).AddMinutes(30),
                Location = "Hlavná telocvičňa",
                Type = TrainingType.Conditioning,
                CoachNote = "Zameranie: rýchlosť a mobilita.",
                CreatedAt = now
            },
            new()
            {
                Id = Guid.NewGuid(),
                TeamId = teams.Count > 1 ? teams[1].Id : teams.First().Id,
                SeasonId = activeSeason.Id,
                Title = $"{SamplePrefix} Technický tréning B-tím",
                StartTime = now.AddDays(-1).Date.AddHours(16),
                EndTime = now.AddDays(-1).Date.AddHours(17).AddMinutes(30),
                Location = "Vedľajšia telocvičňa",
                Type = TrainingType.Technical,
                CoachNote = "Práca s loptou a prihrávky.",
                CreatedAt = now
            },
            new()
            {
                Id = Guid.NewGuid(),
                TeamId = teams.Count > 2 ? teams[2].Id : teams.First().Id,
                SeasonId = activeSeason.Id,
                Title = $"{SamplePrefix} Zápasová príprava",
                StartTime = now.AddDays(2).Date.AddHours(17),
                EndTime = now.AddDays(2).Date.AddHours(18).AddMinutes(45),
                Location = "Štadión - hlavné ihrisko",
                Type = TrainingType.Match,
                CoachNote = "Modelové herné situácie.",
                CreatedAt = now
            }
        };

        _context.TrainingEvents.AddRange(trainings);
        await _context.SaveChangesAsync();
        return trainings;
    }

    private async Task EnsureSampleAttendanceAsync(List<TrainingEvent> trainings, List<ApplicationUser> sampleChildren, ApplicationUser? markedByUser)
    {
        var pastTrainings = trainings
            .Where(t => t.StartTime <= DateTime.UtcNow.AddHours(-1))
            .ToList();

        foreach (var training in pastTrainings)
        {
            var teamMemberIds = await _context.TeamMembers
                .Where(tm => tm.TeamId == training.TeamId && sampleChildren.Select(c => c.Id).Contains(tm.UserId))
                .Select(tm => tm.UserId)
                .ToListAsync();

            for (var i = 0; i < teamMemberIds.Count; i++)
            {
                var childId = teamMemberIds[i];
                var exists = await _context.AttendanceRecords
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
                    Id = Guid.NewGuid(),
                    TrainingEventId = training.Id,
                    ChildId = childId,
                    Status = status,
                    Note = status == AttendanceStatus.Absent ? $"{SamplePrefix} Krátkodobá absencia" : null,
                    CoachComment = status == AttendanceStatus.Present ? $"{SamplePrefix} Dobrá aktivita na tréningu" : null,
                    MarkedByUserId = markedByUser?.Id,
                    RecordedAt = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task EnsureSampleAbsenceRequestsAsync(List<TrainingEvent> trainings, List<ApplicationUser> sampleChildren, ApplicationUser? parent)
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
            .Where(tm => tm.TeamId == upcomingTraining.TeamId && sampleChildren.Select(c => c.Id).Contains(tm.UserId))
            .Select(tm => tm.UserId)
            .FirstOrDefaultAsync();

        if (childId == null)
            return;

        var existingSampleExcuse = await _context.AbsenceRequests
            .AnyAsync(ar => ar.ParentId == parent.Id && ar.ChildId == childId && ar.TrainingEventId == upcomingTraining.Id && ar.Reason != null && ar.Reason.StartsWith(SamplePrefix));

        if (existingSampleExcuse)
            return;

        _context.AbsenceRequests.Add(new AbsenceRequest
        {
            Id = Guid.NewGuid(),
            ChildId = childId,
            ParentId = parent.Id,
            TrainingEventId = upcomingTraining.Id,
            DateFrom = upcomingTraining.StartTime,
            DateTo = upcomingTraining.EndTime,
            Reason = $"{SamplePrefix} Školská akcia mimo mesta",
            Note = "Návrat na ďalší tréning podľa plánu.",
            Status = AbsenceRequestStatus.Received,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
    }

    private const string RichSamplePrefix = "[SAMPLE-HIST]";

    private async Task EnsureRichSampleHistoryAsync(
        List<Team> teams,
        List<ApplicationUser> sampleChildren,
        ApplicationUser? markedByUser,
        ApplicationUser? parent)
    {
        if (!teams.Any() || !sampleChildren.Any())
            return;

        var alreadySeeded = await _context.TrainingEvents
            .AnyAsync(t => t.Title.StartsWith(RichSamplePrefix));
        if (alreadySeeded)
            return;

        var activeSeason = await _context.Seasons.FirstOrDefaultAsync(s => s.IsActive);
        if (activeSeason == null)
            return;

        var rng = new Random(42);
        var now = DateTime.UtcNow;
        var startWindow = now.AddDays(-90);

        var trainings = new List<TrainingEvent>();
        var attendances = new List<AttendanceRecord>();
        var excuses = new List<AbsenceRequest>();

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
        var titles = new[]
        {
            "Príprava na zápas", "Kondičný tréning", "Technika - prihrávky",
            "Hranie 5 na 5", "Regenerácia", "Špeciálne situácie", "Rýchlosť a obratnosť"
        };

        var teamMemberCache = new Dictionary<Guid, List<string>>();
        foreach (var team in teams)
        {
            var members = await _context.TeamMembers
                .Where(tm => tm.TeamId == team.Id && sampleChildren.Select(c => c.Id).Contains(tm.UserId))
                .Select(tm => tm.UserId)
                .ToListAsync();
            teamMemberCache[team.Id] = members;
        }

        // Generuj treningy pre kazdy tim, 2-krat tyzdenne za poslednych 90 dni
        for (var day = startWindow.Date; day <= now.Date.AddDays(7); day = day.AddDays(1))
        {
            // Trening Pondelok a Streda alebo Utorok a Stvrtok podla tima
            for (var teamIndex = 0; teamIndex < teams.Count; teamIndex++)
            {
                var team = teams[teamIndex];
                var dayOfWeek = (int)day.DayOfWeek;
                var teamDaysA = teamIndex % 2 == 0 ? new[] { 1, 3 } : new[] { 2, 4 }; // Po/St alebo Ut/St
                if (!teamDaysA.Contains(dayOfWeek))
                    continue;

                var hour = 16 + (teamIndex % 3);
                var startTime = day.AddHours(hour);
                var type = typeRotation[(teamIndex + day.DayOfYear) % typeRotation.Length];
                var title = $"{RichSamplePrefix} {titles[(teamIndex + day.DayOfYear) % titles.Length]} - {team.ShortName}";

                var training = new TrainingEvent
                {
                    Id = Guid.NewGuid(),
                    TeamId = team.Id,
                    SeasonId = activeSeason.Id,
                    Title = title,
                    StartTime = startTime,
                    EndTime = startTime.AddMinutes(90),
                    Location = locations[(teamIndex + day.DayOfYear) % locations.Length],
                    Type = type,
                    CoachNote = null,
                    CreatedAt = startTime.AddDays(-7),
                    IsLocked = false
                };
                trainings.Add(training);

                // Dochadzka len pre minule treningy
                if (training.StartTime > now)
                    continue;

                var members = teamMemberCache[team.Id];
                foreach (var childId in members)
                {
                    // Realisticka distribucia: 70% Present, 18% Excused, 12% Absent
                    var roll = rng.Next(100);
                    var status = roll < 70 ? AttendanceStatus.Present
                        : roll < 88 ? AttendanceStatus.Excused
                        : AttendanceStatus.Absent;

                    attendances.Add(new AttendanceRecord
                    {
                        Id = Guid.NewGuid(),
                        TrainingEventId = training.Id,
                        ChildId = childId,
                        Status = status,
                        Note = status == AttendanceStatus.Absent ? $"{RichSamplePrefix} Neospravedlnená absencia" : null,
                        MarkedByUserId = markedByUser?.Id,
                        RecordedAt = training.StartTime.AddHours(2)
                    });
                }
            }
        }

        // Ospravedlnenky: 12 vzoriek pre nadchadzajuce treningy
        if (parent != null)
        {
            var futureTrainings = trainings
                .Where(t => t.StartTime > now)
                .OrderBy(t => t.StartTime)
                .Take(12)
                .ToList();

            var reasons = new[]
            {
                "Choroba - chrípka", "Rodinná dovolenka", "Návšteva u lekára",
                "Školský výlet", "Príprava na test", "Iný šport - turnaj",
                "Súrodenecké narodeniny", "Rodinná oslava", "Doprava nedostupná"
            };

            foreach (var training in futureTrainings)
            {
                var members = teamMemberCache[training.TeamId];
                if (members.Count == 0) continue;

                var childId = members[rng.Next(members.Count)];
                excuses.Add(new AbsenceRequest
                {
                    Id = Guid.NewGuid(),
                    ChildId = childId,
                    ParentId = parent.Id,
                    TrainingEventId = training.Id,
                    DateFrom = training.StartTime,
                    DateTo = training.EndTime,
                    Reason = $"{RichSamplePrefix} {reasons[rng.Next(reasons.Length)]}",
                    Status = AbsenceRequestStatus.Received,
                    CreatedAt = now.AddDays(-rng.Next(1, 14))
                });
            }
        }

        _context.TrainingEvents.AddRange(trainings);
        await _context.SaveChangesAsync();

        _context.AttendanceRecords.AddRange(attendances);
        _context.AbsenceRequests.AddRange(excuses);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Seeded {Trainings} trainings, {Attendances} attendance records, {Excuses} excuses for sample history.",
            trainings.Count, attendances.Count, excuses.Count);
    }

    private async Task EnsureSampleAnnouncementsAsync(ApplicationUser? author, List<Team> teams)
    {
        if (author == null)
            return;

        var definitions = new List<Announcement>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Title = $"{SamplePrefix} Úvodný informačný oznam",
                Content = "Vitajte v testovacom prostredí ZISK. Tento oznam slúži na ukážku práce so systémovými oznamami.",
                TargetTeamId = null,
                TargetAudience = TargetAudience.All,
                Priority = AnnouncementPriority.Medium,
                IsPinned = true,
                ValidUntil = DateTime.UtcNow.AddDays(30),
                AuthorUserId = author.Id,
                PublishDate = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                Title = $"{SamplePrefix} Organizačné pokyny pre rodičov",
                Content = "Prosíme rodičov o kontrolu termínov tréningov a ospravedlneniek v aplikácii.",
                TargetTeamId = teams.First().Id,
                TargetAudience = TargetAudience.Parents,
                Priority = AnnouncementPriority.High,
                IsPinned = false,
                ValidUntil = DateTime.UtcNow.AddDays(21),
                AuthorUserId = author.Id,
                PublishDate = DateTime.UtcNow.AddHours(-3)
            },
            new()
            {
                Id = Guid.NewGuid(),
                Title = $"{SamplePrefix} Pripomienka pred tréningom",
                Content = "Nezabudnite na pitný režim a včasný príchod aspoň 15 minút pred začiatkom tréningu.",
                TargetTeamId = teams.Count > 1 ? teams[1].Id : teams.First().Id,
                TargetAudience = TargetAudience.Athletes,
                Priority = AnnouncementPriority.Low,
                IsPinned = false,
                ValidUntil = DateTime.UtcNow.AddDays(14),
                AuthorUserId = author.Id,
                PublishDate = DateTime.UtcNow.AddHours(-1)
            }
        };

        foreach (var announcement in definitions)
        {
            var exists = await _context.Announcements.AnyAsync(a => a.Title == announcement.Title);
            if (!exists)
                _context.Announcements.Add(announcement);
        }

        await _context.SaveChangesAsync();
    }

    private async Task EnsureSampleDocumentsAsync()
    {
        var documents = new List<Document>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Title = $"{SamplePrefix} Klubový poriadok",
                FilePath = "/uploads/documents/sample-klubovy-poriadok.pdf",
                Category = DocumentCategory.General,
                TargetRoleId = null,
                UploadedAt = DateTime.UtcNow.AddDays(-5)
            },
            new()
            {
                Id = Guid.NewGuid(),
                Title = $"{SamplePrefix} Vzor rodičovského súhlasu",
                FilePath = "/uploads/documents/sample-rodicovsky-suhlas.docx",
                Category = DocumentCategory.Contract,
                TargetRoleId = null,
                UploadedAt = DateTime.UtcNow.AddDays(-3)
            },
            new()
            {
                Id = Guid.NewGuid(),
                Title = $"{SamplePrefix} Týždenný tréningový plán",
                FilePath = "/uploads/documents/sample-treningovy-plan.xlsx",
                Category = DocumentCategory.TrainingPlan,
                TargetRoleId = null,
                UploadedAt = DateTime.UtcNow.AddDays(-1)
            }
        };

        foreach (var document in documents)
        {
            var exists = await _context.Documents.AnyAsync(d => d.Title == document.Title);
            if (!exists)
                _context.Documents.Add(document);
        }

        await _context.SaveChangesAsync();
    }
}
