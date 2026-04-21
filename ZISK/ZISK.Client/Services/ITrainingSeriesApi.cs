using Refit;
using ZISK.Shared.DTOs.Trainings;

namespace ZISK.Client.Services;

public interface ITrainingSeriesApi
{
    [Get("/api/training-series")]
    Task<List<TrainingSeriesDto>> GetSeriesAsync([Query] Guid? teamId = null, [Query] Guid? seasonId = null);

    [Get("/api/training-series/{id}")]
    Task<TrainingSeriesDto> GetSeriesAsync(Guid id);

    [Post("/api/training-series")]
    Task<TrainingSeriesDto> CreateSeriesAsync([Body] CreateTrainingSeriesRequest request);

    [Put("/api/training-series/{id}")]
    Task<TrainingSeriesDto> UpdateSeriesAsync(Guid id, [Body] UpdateTrainingSeriesRequest request);

    [Delete("/api/training-series/{id}")]
    Task DeleteSeriesAsync(Guid id);

    [Post("/api/training-series/{id}/generate")]
    Task<int> GenerateInstancesAsync(Guid id, [Body] GenerateInstancesRequest request);
}
