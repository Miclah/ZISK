using ZISK.Shared.DTOs.Trainings;

namespace ZISK.Services;

public interface ITrainingSeriesService
{
    Task<List<TrainingSeriesDto>> GetSeriesAsync(Guid? teamId = null, Guid? seasonId = null);
    Task<TrainingSeriesDto> GetSeriesAsync(Guid id);
    Task<TrainingSeriesDto> CreateSeriesAsync(CreateTrainingSeriesRequest request, string coachId);
    Task<TrainingSeriesDto> UpdateSeriesAsync(Guid id, UpdateTrainingSeriesRequest request);
    Task DeleteSeriesAsync(Guid id);
    Task<int> GenerateInstancesAsync(Guid seriesId, DateOnly from, DateOnly to);
}
