using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
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

        var admin = await EnsureUserAsync("admin@zisk.sk", "admin123", "Admin", "Admin", "ZISK");
        var coach = await EnsureUserAsync("trener@zisk.sk", "trener123", "Coach", "Ján", "Tréner");
        var parent = await EnsureUserAsync("rodic@zisk.sk", "rodic123", "Parent", "Peter", "Rodič");
        var child = await EnsureUserAsync("dieta@zisk.sk", "dieta123", "Child", "Tomáš", "Dieťa");

        await SeedTeamsAsync();
        await EnsureCoachTeamsTableAsync();
        await SeedChildrenAsync(parent);
        await EnsureChildProfileForSeedChildAsync(child, parent);
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
            {
                await _roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    private async Task EnsureCoachTeamsTableAsync()
    {
        await _context.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'[dbo].[CoachTeams]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CoachTeams](
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [CoachId] NVARCHAR(450) NOT NULL,
        [TeamId] UNIQUEIDENTIFIER NOT NULL,
        [IsPrimary] BIT NOT NULL CONSTRAINT [DF_CoachTeams_IsPrimary] DEFAULT(0),
        [AssignedAt] DATETIME2 NOT NULL CONSTRAINT [DF_CoachTeams_AssignedAt] DEFAULT(SYSUTCDATETIME()),
        CONSTRAINT [PK_CoachTeams] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CoachTeams_AspNetUsers_CoachId] FOREIGN KEY ([CoachId]) REFERENCES [AspNetUsers]([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CoachTeams_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [Teams]([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_CoachTeams_CoachId] ON [dbo].[CoachTeams]([CoachId]);
    CREATE INDEX [IX_CoachTeams_TeamId] ON [dbo].[CoachTeams]([TeamId]);
END");
    }

    private async Task EnsureChildProfileForSeedChildAsync(ApplicationUser? childUser, ApplicationUser? parent)
    {
        if (childUser == null || string.IsNullOrWhiteSpace(childUser.Email))
            return;

        var childProfile = await _context.ChildProfiles
            .FirstOrDefaultAsync(c => c.Email == childUser.Email);

        if (childProfile == null)
        {
            childProfile = await _context.ChildProfiles
                .OrderBy(c => c.CreatedAt)
                .FirstOrDefaultAsync(c => c.IsActive && string.IsNullOrWhiteSpace(c.Email));

            if (childProfile != null)
            {
                childProfile.Email = childUser.Email;
                childProfile.FirstName = childUser.FirstName;
                childProfile.LastName = childUser.LastName;
            }
            else
            {
                var teamId = await _context.Teams
                    .Where(t => t.IsActive)
                    .Select(t => (Guid?)t.Id)
                    .FirstOrDefaultAsync();

                childProfile = new ChildProfile
                {
                    Id = Guid.NewGuid(),
                    FirstName = childUser.FirstName,
                    LastName = childUser.LastName,
                    DateOfBirth = new DateOnly(2012, 1, 1),
                    Email = childUser.Email,
                    TeamId = teamId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.ChildProfiles.Add(childProfile);
            }

            await _context.SaveChangesAsync();
        }

        if (parent != null)
        {
            var hasLink = await _context.ParentChildren
                .AnyAsync(pc => pc.ParentId == parent.Id && pc.ChildId == childProfile.Id);

            if (!hasLink)
            {
                _context.ParentChildren.Add(new ParentChild
                {
                    ParentId = parent.Id,
                    ChildId = childProfile.Id,
                    IsPrimary = false
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
        {
            await _userManager.AddToRoleAsync(user, role);
        }

        return user;
    }

    private async Task SeedTeamsAsync()
    {
        if (await _context.Teams.AnyAsync())
        {
            return;
        }

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

    private async Task SeedChildrenAsync(ApplicationUser? parent)
    {
        if (await _context.ChildProfiles.AnyAsync())
        {
            return;
        }

        var aTeam = await _context.Teams.FirstOrDefaultAsync(t => t.Name == "A-tím");
        var bTeam = await _context.Teams.FirstOrDefaultAsync(t => t.Name == "B-tím");
        var youth = await _context.Teams.FirstOrDefaultAsync(t => t.Name == "Žiaci");

        var children = new List<ChildProfile>
        {
            new() { Id = Guid.NewGuid(), FirstName = "Ján", LastName = "Novák", DateOfBirth = new DateOnly(2010, 5, 15), TeamId = aTeam?.Id, IsActive = true },
            new() { Id = Guid.NewGuid(), FirstName = "Peter", LastName = "Horváth", DateOfBirth = new DateOnly(2011, 3, 22), TeamId = aTeam?.Id, IsActive = true },
            new() { Id = Guid.NewGuid(), FirstName = "Mária", LastName = "Kováčová", DateOfBirth = new DateOnly(2012, 7, 8), TeamId = bTeam?.Id, IsActive = true },
            new() { Id = Guid.NewGuid(), FirstName = "Lucia", LastName = "Varga", DateOfBirth = new DateOnly(2013, 11, 30), TeamId = youth?.Id, IsActive = true },
            new() { Id = Guid.NewGuid(), FirstName = "Martin", LastName = "Balog", DateOfBirth = new DateOnly(2010, 1, 5), TeamId = aTeam?.Id, IsActive = true }
        };

        _context.ChildProfiles.AddRange(children);
        await _context.SaveChangesAsync();

        if (parent != null)
        {
            _context.ParentChildren.Add(new ParentChild
            {
                ParentId = parent.Id,
                ChildId = children.First().Id,
                IsPrimary = true
            });
            await _context.SaveChangesAsync();
        }
    }

    private async Task EnsureCoachTeamAssignmentsAsync(ApplicationUser? coach)
    {
        if (coach == null)
        {
            return;
        }

        var hasCoachTeamsTable = await CoachTeamsTableExistsAsync();
        if (!hasCoachTeamsTable)
        {
            _logger.LogWarning("CoachTeams table is missing in database. Skipping coach-team seed assignments.");
            return;
        }

        var activeTeamIds = await _context.Teams
            .Where(t => t.IsActive)
            .Select(t => t.Id)
            .ToListAsync();

        if (!activeTeamIds.Any())
        {
            return;
        }

        List<Guid> assignedTeamIds;
        bool hasPrimary;

        try
        {
            assignedTeamIds = await _context.CoachTeams
                .Where(ct => ct.CoachId == coach.Id)
                .Select(ct => ct.TeamId)
                .ToListAsync();

            hasPrimary = await _context.CoachTeams
                .AnyAsync(ct => ct.CoachId == coach.Id && ct.IsPrimary);
        }
        catch (SqlException ex) when (ex.Number == 208 && ex.Message.Contains("CoachTeams", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "CoachTeams table is missing. Skipping coach-team seed assignments.");
            return;
        }

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

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (SqlException ex) when (ex.Number == 208 && ex.Message.Contains("CoachTeams", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(ex, "CoachTeams table is missing while saving coach-team assignments. Skipping.");
        }
    }

    private async Task SeedSampleDataAsync(ApplicationUser? admin, ApplicationUser? defaultCoach, ApplicationUser? defaultParent)
    {
        var teams = await _context.Teams
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync();

        if (!teams.Any())
        {
            return;
        }

        var sampleUsers = await EnsureSampleUsersAsync();
        var sampleChildren = await EnsureSampleChildrenAsync(teams, sampleUsers);

        await EnsureSampleParentLinksAsync(defaultParent, sampleUsers, sampleChildren);
        await EnsureSampleCoachAssignmentsAsync(defaultCoach, sampleUsers, teams);

        var sampleTrainings = await EnsureSampleTrainingsAsync(teams);
        var attendanceUser = defaultCoach ?? sampleUsers.Coaches.FirstOrDefault() ?? admin;

        await EnsureSampleAttendanceAsync(sampleTrainings, sampleChildren, attendanceUser);

        var sampleParent = sampleUsers.Parents.FirstOrDefault() ?? defaultParent;
        await EnsureSampleAbsenceRequestsAsync(sampleTrainings, sampleChildren, sampleParent);

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

        var coach1 = await EnsureUserAsync("coach.marek.sample@zisk.sk", "sample123", "Coach", "Marek", "SampleCoach");
        var coach2 = await EnsureUserAsync("coach.lukas.sample@zisk.sk", "sample123", "Coach", "Lukáš", "SampleCoach");
        var parent1 = await EnsureUserAsync("parent.jana.sample@zisk.sk", "sample123", "Parent", "Jana", "SampleParent");
        var parent2 = await EnsureUserAsync("parent.milan.sample@zisk.sk", "sample123", "Parent", "Milan", "SampleParent");
        var athlete1 = await EnsureUserAsync("athlete.adam.sample@zisk.sk", "sample123", "Athlete", "Adam", "SampleAthlete");
        var child1 = await EnsureUserAsync("child.nina.sample@zisk.sk", "sample123", "Child", "Nina", "SampleChild");

        if (coach1 != null) coaches.Add(coach1);
        if (coach2 != null) coaches.Add(coach2);
        if (parent1 != null) parents.Add(parent1);
        if (parent2 != null) parents.Add(parent2);
        if (athlete1 != null) athletes.Add(athlete1);
        if (child1 != null) children.Add(child1);

        return new SampleUserGroup(coaches, parents, athletes, children);
    }

    private async Task<List<ChildProfile>> EnsureSampleChildrenAsync(List<Team> teams, SampleUserGroup sampleUsers)
    {
        var children = new List<ChildProfile>();

        var athleteTeamId = teams.First().Id;
        var childTeamId = teams.Count > 1 ? teams[1].Id : teams.First().Id;
        var thirdTeamId = teams.Count > 2 ? teams[2].Id : teams.First().Id;

        var childDefinitions = new[]
        {
            new { FirstName = "Adam", LastName = "SampleAthlete", DateOfBirth = new DateOnly(2011, 4, 10), TeamId = athleteTeamId, Email = sampleUsers.Athletes.FirstOrDefault()?.Email },
            new { FirstName = "Nina", LastName = "SampleChild", DateOfBirth = new DateOnly(2012, 9, 3), TeamId = childTeamId, Email = sampleUsers.Children.FirstOrDefault()?.Email },
            new { FirstName = "Ema", LastName = "SampleData", DateOfBirth = new DateOnly(2013, 2, 17), TeamId = thirdTeamId, Email = (string?)null },
            new { FirstName = "Filip", LastName = "SampleData", DateOfBirth = new DateOnly(2010, 12, 5), TeamId = athleteTeamId, Email = (string?)null }
        };

        foreach (var definition in childDefinitions)
        {
            ChildProfile? child;

            if (!string.IsNullOrWhiteSpace(definition.Email))
            {
                child = await _context.ChildProfiles
                    .FirstOrDefaultAsync(c => c.Email == definition.Email);
            }
            else
            {
                child = await _context.ChildProfiles
                    .FirstOrDefaultAsync(c => c.FirstName == definition.FirstName && c.LastName == definition.LastName && c.DateOfBirth == definition.DateOfBirth);
            }

            if (child == null)
            {
                child = new ChildProfile
                {
                    Id = Guid.NewGuid(),
                    FirstName = definition.FirstName,
                    LastName = definition.LastName,
                    DateOfBirth = definition.DateOfBirth,
                    TeamId = definition.TeamId,
                    Email = definition.Email,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.ChildProfiles.Add(child);
            }
            else
            {
                child.TeamId = definition.TeamId;
                child.IsActive = true;

                if (!string.IsNullOrWhiteSpace(definition.Email))
                {
                    child.Email = definition.Email;
                }
            }

            children.Add(child);
        }

        await _context.SaveChangesAsync();
        return children;
    }

    private async Task EnsureSampleParentLinksAsync(ApplicationUser? defaultParent, SampleUserGroup sampleUsers, List<ChildProfile> sampleChildren)
    {
        var parentLinks = new List<(string ParentId, Guid ChildId, bool IsPrimary)>();

        if (defaultParent != null && sampleChildren.Any())
        {
            parentLinks.Add((defaultParent.Id, sampleChildren[0].Id, true));
        }

        var sampleParent1 = sampleUsers.Parents.ElementAtOrDefault(0);
        var sampleParent2 = sampleUsers.Parents.ElementAtOrDefault(1);

        if (sampleParent1 != null && sampleChildren.Count > 1)
        {
            parentLinks.Add((sampleParent1.Id, sampleChildren[1].Id, true));
        }

        if (sampleParent2 != null)
        {
            if (sampleChildren.Count > 2)
            {
                parentLinks.Add((sampleParent2.Id, sampleChildren[2].Id, true));
            }

            if (sampleChildren.Count > 3)
            {
                parentLinks.Add((sampleParent2.Id, sampleChildren[3].Id, false));
            }
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
        if (!await CoachTeamsTableExistsAsync())
        {
            return;
        }

        var assignments = new List<(ApplicationUser Coach, Guid TeamId, bool IsPrimary)>();

        if (defaultCoach != null)
        {
            assignments.Add((defaultCoach, teams.First().Id, true));
        }

        var sampleCoach1 = sampleUsers.Coaches.ElementAtOrDefault(0);
        var sampleCoach2 = sampleUsers.Coaches.ElementAtOrDefault(1);

        if (sampleCoach1 != null)
        {
            assignments.Add((sampleCoach1, teams.First().Id, true));
            if (teams.Count > 1)
            {
                assignments.Add((sampleCoach1, teams[1].Id, false));
            }
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

            if (exists)
            {
                continue;
            }

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
        {
            return existingSampleTrainings;
        }

        var now = DateTime.UtcNow;
        var trainings = new List<TrainingEvent>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TeamId = teams.First().Id,
                Title = $"{SamplePrefix} Kondičný tréning A-tím",
                StartTime = now.AddDays(-3).Date.AddHours(17),
                EndTime = now.AddDays(-3).Date.AddHours(18).AddMinutes(30),
                Location = "Hlavná telocvičňa",
                Type = TrainingType.Conditioning,
                CoachNote = "Zameranie: rýchlosť a mobilita.",
                IsLocked = false,
                CreatedAt = now
            },
            new()
            {
                Id = Guid.NewGuid(),
                TeamId = teams.Count > 1 ? teams[1].Id : teams.First().Id,
                Title = $"{SamplePrefix} Technický tréning B-tím",
                StartTime = now.AddDays(-1).Date.AddHours(16),
                EndTime = now.AddDays(-1).Date.AddHours(17).AddMinutes(30),
                Location = "Vedľajšia telocvičňa",
                Type = TrainingType.Technical,
                CoachNote = "Práca s loptou a prihrávky.",
                IsLocked = false,
                CreatedAt = now
            },
            new()
            {
                Id = Guid.NewGuid(),
                TeamId = teams.Count > 2 ? teams[2].Id : teams.First().Id,
                Title = $"{SamplePrefix} Zápasová príprava",
                StartTime = now.AddDays(2).Date.AddHours(17),
                EndTime = now.AddDays(2).Date.AddHours(18).AddMinutes(45),
                Location = "Štadión - hlavné ihrisko",
                Type = TrainingType.Match,
                CoachNote = "Modelové herné situácie.",
                IsLocked = false,
                CreatedAt = now
            }
        };

        _context.TrainingEvents.AddRange(trainings);
        await _context.SaveChangesAsync();
        return trainings;
    }

    private async Task EnsureSampleAttendanceAsync(List<TrainingEvent> trainings, List<ChildProfile> sampleChildren, ApplicationUser? markedByUser)
    {
        var pastTrainings = trainings
            .Where(t => t.StartTime <= DateTime.UtcNow.AddHours(-1))
            .ToList();

        foreach (var training in pastTrainings)
        {
            var teamChildren = sampleChildren
                .Where(c => c.TeamId == training.TeamId)
                .ToList();

            for (var i = 0; i < teamChildren.Count; i++)
            {
                var child = teamChildren[i];
                var exists = await _context.AttendanceRecords
                    .AnyAsync(ar => ar.TrainingEventId == training.Id && ar.ChildId == child.Id);

                if (exists)
                {
                    continue;
                }

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
                    ChildId = child.Id,
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

    private async Task EnsureSampleAbsenceRequestsAsync(List<TrainingEvent> trainings, List<ChildProfile> sampleChildren, ApplicationUser? parent)
    {
        if (parent == null)
        {
            return;
        }

        var upcomingTraining = trainings
            .Where(t => t.StartTime > DateTime.UtcNow)
            .OrderBy(t => t.StartTime)
            .FirstOrDefault();

        if (upcomingTraining == null)
        {
            return;
        }

        var child = sampleChildren.FirstOrDefault(c => c.TeamId == upcomingTraining.TeamId) ?? sampleChildren.FirstOrDefault();
        if (child == null)
        {
            return;
        }

        var existingSampleExcuse = await _context.AbsenceRequests
            .AnyAsync(ar => ar.ParentId == parent.Id && ar.ChildId == child.Id && ar.TrainingEventId == upcomingTraining.Id && ar.Reason != null && ar.Reason.StartsWith(SamplePrefix));

        if (existingSampleExcuse)
        {
            return;
        }

        _context.AbsenceRequests.Add(new AbsenceRequest
        {
            Id = Guid.NewGuid(),
            ChildId = child.Id,
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

    private async Task EnsureSampleAnnouncementsAsync(ApplicationUser? author, List<Team> teams)
    {
        if (author == null)
        {
            return;
        }

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
            var exists = await _context.Announcements
                .AnyAsync(a => a.Title == announcement.Title);

            if (!exists)
            {
                _context.Announcements.Add(announcement);
            }
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
            var exists = await _context.Documents
                .AnyAsync(d => d.Title == document.Title);

            if (!exists)
            {
                _context.Documents.Add(document);
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task<bool> CoachTeamsTableExistsAsync()
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT CASE WHEN OBJECT_ID(N'[dbo].[CoachTeams]', N'U') IS NULL THEN 0 ELSE 1 END";
            var result = await command.ExecuteScalarAsync();

            return result is not null && Convert.ToInt32(result) == 1;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
