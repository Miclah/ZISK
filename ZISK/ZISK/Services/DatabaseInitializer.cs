using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ZISK.Data;
using ZISK.Data.Entities;

namespace ZISK.Services;

public class DatabaseInitializer
{
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
        await SeedChildrenAsync(parent);
        await EnsureChildProfileForSeedChildAsync(child, parent);
        await EnsureCoachTeamAssignmentsAsync(coach);
    }

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
