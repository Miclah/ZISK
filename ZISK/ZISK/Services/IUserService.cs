using System.Security.Claims;
using ZISK.Shared.DTOs.Users;

namespace ZISK.Services;

public interface IUserService
{
    Task<List<UserListDto>> GetUsersAsync(string? role);
    Task<UserDto> GetUserAsync(string id);
    Task<List<ParentOptionDto>> GetParentsAsync();
    Task<UserDto> CreateUserAsync(CreateUserRequest request, ClaimsPrincipal user);
    Task<UserDto> UpdateUserAsync(string id, UpdateUserRequest request, ClaimsPrincipal user);
    Task ToggleStatusAsync(string id, ClaimsPrincipal user);
    Task<List<UserListDto>> GetCoachesAsync();
    Task AssignTeamAsync(string userId, Guid teamId, bool isPrimary);
    Task RemoveTeamAsync(string userId, Guid teamId);
    Task DeleteUserAsync(string id, ClaimsPrincipal user);
}
