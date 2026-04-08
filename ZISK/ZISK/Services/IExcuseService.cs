using System.Security.Claims;
using ZISK.Shared.DTOs.Excuses;
using ExcuseStatus = ZISK.Shared.Enums.ExcuseStatus;

namespace ZISK.Services;

public interface IExcuseService
{
    Task<List<ExcuseListDto>> GetExcusesAsync(ExcuseStatus? status, ClaimsPrincipal user);
    Task<ExcuseDto> GetExcuseAsync(Guid id, ClaimsPrincipal user);
    Task<ExcuseDto> CreateExcuseAsync(CreateExcuseRequest request, ClaimsPrincipal user);
    Task UpdateStatusAsync(Guid id, UpdateExcuseStatusRequest request, ClaimsPrincipal user);
    Task UpdateExcuseAsync(Guid id, UpdateExcuseRequest request, ClaimsPrincipal user);
    Task DeleteExcuseAsync(Guid id, ClaimsPrincipal user);
    Task<List<ExcuseListDto>> GetMyExcusesAsync(ClaimsPrincipal user);
    Task<int> GetPendingCountAsync(ClaimsPrincipal user);
}
