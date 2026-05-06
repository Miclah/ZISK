using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ZISK.Data;

namespace ZISK.Services;

public class TeamAccessService : ITeamAccessService
{
    private readonly ApplicationDbContext _context;

    public TeamAccessService(ApplicationDbContext context)
    {
        _context = context;
    }

    // Return value convention: null means unrestricted access (Admin), empty set means no access at all,
    // non-empty set contains the specific team IDs this user is allowed to see.
    // Every caller must handle all three cases differently.
    public async Task<HashSet<Guid>?> GetAccessibleTeamIdsAsync(ClaimsPrincipal user)
    {
        if (user.IsInRole("Admin"))
            return null;

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = new HashSet<Guid>();

        if (string.IsNullOrEmpty(userId))
            return result;

        if (user.IsInRole("Coach"))
        {
            var coachTeams = await _context.CoachTeams
                .Where(ct => ct.CoachId == userId)
                .Select(ct => ct.TeamId)
                .ToListAsync();

            foreach (var teamId in coachTeams)
                result.Add(teamId);
        }

        if (user.IsInRole("Parent"))
        {
            var childIds = await _context.ParentChildren
                .Where(pc => pc.ParentId == userId)
                .Select(pc => pc.ChildId)
                .ToListAsync();

            var parentTeams = await _context.TeamMembers
                .Where(tm => childIds.Contains(tm.UserId))
                .Select(tm => tm.TeamId)
                .Distinct()
                .ToListAsync();

            foreach (var teamId in parentTeams)
                result.Add(teamId);
        }

        if (user.IsInRole("Athlete") || user.IsInRole("Child"))
        {
            var ownTeams = await _context.TeamMembers
                .Where(tm => tm.UserId == userId)
                .Select(tm => tm.TeamId)
                .Distinct()
                .ToListAsync();

            foreach (var teamId in ownTeams)
                result.Add(teamId);
        }

        return result;
    }
}
