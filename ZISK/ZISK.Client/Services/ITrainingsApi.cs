using Refit;
using ZISK.Shared.DTOs.Trainings;
using ZISK.Shared.Enums;

namespace ZISK.Client.Services;

public interface ITrainingsApi
{
    [Get("/api/trainings")]
    Task<List<TrainingEventDto>> GetTrainingsAsync(
        [Query] Guid? teamId = null, 
        [Query] DateTime? from = null, 
        [Query] DateTime? to = null);

    [Get("/api/trainings/{id}")]
    Task<TrainingEventDetailDto> GetTrainingAsync(Guid id);

    [Post("/api/trainings")]
    Task<TrainingEventDto> CreateTrainingAsync([Body] CreateTrainingEventRequest request);

    [Put("/api/trainings/{id}")]
    Task UpdateTrainingAsync(Guid id, [Body] UpdateTrainingEventRequest request);

    [Put("/api/trainings/{id}/lock")]
    Task LockTrainingAsync(Guid id);

    [Put("/api/trainings/{id}/unlock")]
    Task UnlockTrainingAsync(Guid id);

    [Delete("/api/trainings/{id}")]
    Task DeleteTrainingAsync(Guid id);
}
