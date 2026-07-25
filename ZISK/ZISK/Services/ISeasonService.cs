using ZISK.Shared.DTOs.Seasons;

namespace ZISK.Services;

public interface ISeasonService
{
    Task<List<SeasonDto>> GetSeasonsAsync();
    Task<SeasonDto> GetSeasonAsync(Guid id);
    Task<SeasonDto> CreateSeasonAsync(CreateSeasonRequest request);
    Task<SeasonDto> UpdateSeasonAsync(Guid id, UpdateSeasonRequest request);
    Task DeleteSeasonAsync(Guid id);
    Task<SeasonDto> ActivateAsync(Guid id);
}
