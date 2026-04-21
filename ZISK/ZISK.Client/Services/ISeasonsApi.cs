using Refit;
using ZISK.Shared.DTOs.Seasons;

namespace ZISK.Client.Services;

public interface ISeasonsApi
{
    [Get("/api/seasons")]
    Task<List<SeasonDto>> GetSeasonsAsync();

    [Get("/api/seasons/{id}")]
    Task<SeasonDto> GetSeasonAsync(Guid id);

    [Post("/api/seasons")]
    Task<SeasonDto> CreateSeasonAsync([Body] CreateSeasonRequest request);

    [Put("/api/seasons/{id}")]
    Task<SeasonDto> UpdateSeasonAsync(Guid id, [Body] UpdateSeasonRequest request);

    [Delete("/api/seasons/{id}")]
    Task DeleteSeasonAsync(Guid id);

    [Post("/api/seasons/{id}/activate")]
    Task<SeasonDto> ActivateSeasonAsync(Guid id);
}
