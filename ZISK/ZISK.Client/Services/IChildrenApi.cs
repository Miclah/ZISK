using Refit;

namespace ZISK.Client.Services;

public interface IChildrenApi
{
    [Get("/api/children/my")]
    Task<List<ChildDto>> GetMyChildrenAsync();

    [Get("/api/children")]
    Task<List<ChildDto>> GetAllChildrenAsync();
}

public record ChildDto(string Id, string FirstName, string LastName, Guid? TeamId, string? TeamName, bool IsOwnProfile);
