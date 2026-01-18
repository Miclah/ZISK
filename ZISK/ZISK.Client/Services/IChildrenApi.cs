using Refit;

namespace ZISK.Client.Services;

public interface IChildrenApi
{
    [Get("/api/children/my")]
    Task<List<ChildDto>> GetMyChildrenAsync();

    [Get("/api/children")]
    Task<List<ChildDto>> GetAllChildrenAsync();
}

public record ChildDto(Guid Id, string FirstName, string LastName, string? TeamName);
