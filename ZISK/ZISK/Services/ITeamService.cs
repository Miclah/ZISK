using System.Security.Claims;
using ZISK.Shared.DTOs.Teams;

namespace ZISK.Services;

public interface ITeamService
{
    Task<List<TeamDto>> GetTeamsAsync(bool? activeOnly, ClaimsPrincipal user);
    Task<TeamDetailDto> GetTeamAsync(Guid id, ClaimsPrincipal user);
    Task<TeamDto> CreateTeamAsync(CreateTeamRequest request, ClaimsPrincipal user);
    Task UpdateTeamAsync(Guid id, UpdateTeamRequest request, ClaimsPrincipal user);
    Task DeleteTeamAsync(Guid id, ClaimsPrincipal user);
    Task AddMemberAsync(Guid teamId, Guid childId, ClaimsPrincipal user);
    Task RemoveMemberAsync(Guid teamId, Guid childId, ClaimsPrincipal user);
}
