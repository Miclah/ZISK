using Refit;
using ZISK.Shared.DTOs.Teams;

namespace ZISK.Client.Services;

public interface ITeamsApi
{
    [Get("/api/teams")]
    Task<List<TeamDto>> GetTeamsAsync([Query] bool? activeOnly = true);

    [Get("/api/teams/{id}")]
    Task<TeamDetailDto> GetTeamAsync(Guid id);

    [Post("/api/teams")]
    Task<TeamDto> CreateTeamAsync([Body] CreateTeamRequest request);

    [Put("/api/teams/{id}")]
    Task UpdateTeamAsync(Guid id, [Body] UpdateTeamRequest request);

    [Delete("/api/teams/{id}")]
    Task DeleteTeamAsync(Guid id);

    [Post("/api/teams/{id}/members/{childId}")]
    Task AddMemberAsync(Guid id, Guid childId);

    [Delete("/api/teams/{id}/members/{childId}")]
    Task RemoveMemberAsync(Guid id, Guid childId);
}
