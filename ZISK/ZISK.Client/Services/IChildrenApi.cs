using Refit;
using ZISK.Shared.DTOs.Children;

namespace ZISK.Client.Services;

public interface IChildrenApi
{
    [Get("/api/children/my")]
    Task<List<ChildDto>> GetMyChildrenAsync();

    [Get("/api/children")]
    Task<List<ChildDto>> GetAllChildrenAsync();

    [Post("/api/children")]
    Task<ChildDto> CreateAsync([Body] CreateChildRequest request);

    [Get("/api/children/{childId}")]
    Task<ChildDetailDto> GetDetailAsync(string childId);

    [Put("/api/children/{childId}")]
    Task<ChildDetailDto> UpdateAsync(string childId, [Body] UpdateChildRequest request);

    [Get("/api/children/{childId}/parents")]
    Task<List<ParentDto>> GetParentsAsync(string childId);

    [Delete("/api/children/{childId}/parents/{parentUserId}")]
    Task RemoveParentAsync(string childId, string parentUserId);
}

public record ChildDto(string Id, string FirstName, string LastName, Guid? TeamId, string? TeamName, bool IsOwnProfile, DateOnly? DateOfBirth = null);
