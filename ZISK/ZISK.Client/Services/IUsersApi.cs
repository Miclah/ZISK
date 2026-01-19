using ZISK.Shared.DTOs.Users;
using Refit;

namespace ZISK.Client.Services
{
    public interface IUsersApi
    {
        [Get("/api/users")]
        Task<List<UserListDto>> GetUsersAsync([Query] string? role = null);

        [Get("/api/users/{id}")]
        Task<UserDto> GetUserAsync(string id);

        [Put("/api/users/{id}")]
        Task<UserDto> UpdateUserAsync(string id, UpdateUserRequest request);

        [Post("/api/users")]
        Task<UserDto> CreateUserAsync(CreateUserRequest request);

        [Delete("/api/users/{id}")]
        Task DeleteUserAsync(string id);

        [Post("/api/users/{id}/toggle-status")]
        Task ToggleUserStatusAsync(string id);

        [Get("/api/users/coaches")]
        Task<List<UserListDto>> GetCoachesAsync();

        [Post("/api/users/{userId}/teams/{teamId}")]
        Task AssignTeamAsync(string userId, Guid teamId, [Query] bool isPrimary = false);

        [Delete("/api/users/{userId}/teams/{teamId}")]
        Task RemoveTeamAsync(string userId, Guid teamId);
    }
}