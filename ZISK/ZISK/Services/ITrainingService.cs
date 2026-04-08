using System.Security.Claims;
using ZISK.Shared.DTOs.Trainings;

namespace ZISK.Services;

public interface ITrainingService
{
    Task<List<TrainingEventDto>> GetTrainingsAsync(Guid? teamId, DateTime? from, DateTime? to, ClaimsPrincipal user);
    Task<TrainingEventDetailDto> GetTrainingAsync(Guid id, ClaimsPrincipal user);
    Task<TrainingEventDto> CreateTrainingAsync(CreateTrainingEventRequest request, ClaimsPrincipal user);
    Task UpdateTrainingAsync(Guid id, UpdateTrainingEventRequest request, ClaimsPrincipal user);
    Task LockTrainingAsync(Guid id, ClaimsPrincipal user);
    Task UnlockTrainingAsync(Guid id, ClaimsPrincipal user);
    Task DeleteTrainingAsync(Guid id, ClaimsPrincipal user);
}
