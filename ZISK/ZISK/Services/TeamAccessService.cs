using System.Security.Claims;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ZISK.Data;

namespace ZISK.Services;

public class TeamAccessService : ITeamAccessService
{
    private static bool? _isCoachTeamsAvailable;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TeamAccessService> _logger;

    public TeamAccessService(ApplicationDbContext context, ILogger<TeamAccessService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<HashSet<Guid>?> GetAccessibleTeamIdsAsync(ClaimsPrincipal user)
    {
        if (user.IsInRole("Admin"))
        {
            return null;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = user.FindFirstValue(ClaimTypes.Email);
        var result = new HashSet<Guid>();

        if (string.IsNullOrEmpty(userId))
        {
            return result;
        }

        if (user.IsInRole("Coach"))
        {
            List<Guid> coachTeams;
            if (!await CoachTeamsTableExistsAsync())
            {
                coachTeams = [];
            }
            else
            {
                try
                {
                    coachTeams = await _context.CoachTeams
                        .Where(ct => ct.CoachId == userId)
                        .Select(ct => ct.TeamId)
                        .ToListAsync();
                }
                catch (SqlException ex) when (ex.Number == 208 && ex.Message.Contains("CoachTeams", StringComparison.OrdinalIgnoreCase))
                {
                    _isCoachTeamsAvailable = false;
                    coachTeams = [];
                }
            }

            foreach (var teamId in coachTeams)
            {
                result.Add(teamId);
            }
        }

        if (user.IsInRole("Parent"))
        {
            var parentTeams = await _context.ParentChildren
                .Where(pc => pc.ParentId == userId && pc.Child.TeamId.HasValue && pc.Child.IsActive)
                .Select(pc => pc.Child.TeamId!.Value)
                .Distinct()
                .ToListAsync();

            foreach (var teamId in parentTeams)
            {
                result.Add(teamId);
            }
        }

        if ((user.IsInRole("Athlete") || user.IsInRole("Child")) && !string.IsNullOrWhiteSpace(email))
        {
            var ownTeams = await _context.ChildProfiles
                .Where(c => c.IsActive && c.Email == email && c.TeamId.HasValue)
                .Select(c => c.TeamId!.Value)
                .Distinct()
                .ToListAsync();

            foreach (var teamId in ownTeams)
            {
                result.Add(teamId);
            }
        }

        return result;
    }

    private async Task<bool> CoachTeamsTableExistsAsync()
    {
        if (_isCoachTeamsAvailable.HasValue)
            return _isCoachTeamsAvailable.Value;

        try
        {
            var appliedMigrations = await _context.Database.GetAppliedMigrationsAsync();
            if (!appliedMigrations.Any(m => m.Contains("CreateIdentitySchema", StringComparison.OrdinalIgnoreCase)))
            {
                _isCoachTeamsAvailable = false;
                return false;
            }

            var connection = _context.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;

            if (shouldClose)
                await connection.OpenAsync();

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT CASE WHEN OBJECT_ID(N'[dbo].[CoachTeams]', N'U') IS NULL THEN 0 ELSE 1 END";
                var result = await command.ExecuteScalarAsync();
                _isCoachTeamsAvailable = Convert.ToInt32(result) == 1;
                return _isCoachTeamsAvailable.Value;
            }
            finally
            {
                if (shouldClose)
                    await connection.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to verify CoachTeams table existence for team access.");
            _isCoachTeamsAvailable = false;
            return false;
        }
    }
}
