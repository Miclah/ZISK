using System.Security.Claims;
using ZISK.Shared.DTOs.Trainings;

namespace ZISK.Services;

public interface ITrainingSeriesService
{
    Task<List<TrainingSeriesDto>> GetSeriesAsync(ClaimsPrincipal user, Guid? teamId = null, Guid? seasonId = null);
    Task<TrainingSeriesDto> GetSeriesAsync(Guid id, ClaimsPrincipal user);
    Task<TrainingSeriesDto> CreateSeriesAsync(CreateTrainingSeriesRequest request, string coachId, ClaimsPrincipal user);
    Task<TrainingSeriesDto> UpdateSeriesAsync(Guid id, UpdateTrainingSeriesRequest request, ClaimsPrincipal user);
    Task DeleteSeriesAsync(Guid id, ClaimsPrincipal user);
    Task<int> GenerateInstancesAsync(Guid seriesId, DateOnly from, DateOnly to, ClaimsPrincipal user);
}
