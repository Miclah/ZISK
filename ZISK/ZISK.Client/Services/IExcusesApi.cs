using Refit;
using ZISK.Shared.DTOs.Excuses;
using ZISK.Shared.Enums;

namespace ZISK.Client.Services;

public interface IExcusesApi
{
    [Get("/api/excuses")]
    Task<List<ExcuseListDto>> GetExcusesAsync([Query] ExcuseStatus? status = null);

    [Get("/api/excuses/{id}")]
    Task<ExcuseDto> GetExcuseAsync(Guid id);

    [Get("/api/excuses/my")]
    Task<List<ExcuseListDto>> GetMyExcusesAsync();

    [Get("/api/excuses/pending/count")]
    Task<int> GetPendingCountAsync();

    [Post("/api/excuses")]
    Task<ExcuseDto> CreateExcuseAsync([Body] CreateExcuseRequest request);

    [Put("/api/excuses/{id}/status")]
    Task UpdateStatusAsync(Guid id, [Body] UpdateExcuseStatusRequest request);

    [Delete("/api/excuses/{id}")]
    Task DeleteExcuseAsync(Guid id);
}