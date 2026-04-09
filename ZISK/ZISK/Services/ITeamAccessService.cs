using System.Security.Claims;

namespace ZISK.Services;

public interface ITeamAccessService
{
    Task<HashSet<Guid>?> GetAccessibleTeamIdsAsync(ClaimsPrincipal user);
}
