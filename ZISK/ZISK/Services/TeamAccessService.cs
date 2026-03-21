using System.Security.Claims;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ZISK.Data;

namespace ZISK.Services;

public class TeamAccessService : ITeamAccessService
{
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
            try
            {
                coachTeams = await _context.CoachTeams
                    .Where(ct => ct.CoachId == userId)
                    .Select(ct => ct.TeamId)
                    .ToListAsync();
            }
            catch (SqlException ex) when (ex.Number == 208 && ex.Message.Contains("CoachTeams", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(ex, "CoachTeams table is missing in database while resolving access for coach {UserId}.", userId);
                coachTeams = [];
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
}
