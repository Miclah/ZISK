namespace ZISK.Shared.DTOs.Users
{
    public record UserDto(
        string Id,
        string FirstName,
        string LastName,
        string Email,
        string Role,
        bool IsActive,
        DateTime CreatedAt,
        List<UserTeamDto> Teams
    );

    public record UserTeamDto(
        Guid TeamId,
        string TeamName,
        bool IsPrimary
    );

    public record UserListDto(
        string Id,
        string FirstName,
        string LastName,
        string Email,
        string Role,
        bool IsActive
    );

    public record UpdateUserRequest(
        string? FirstName,
        string? LastName,
        string? Role,
        bool? IsActive,
        List<Guid>? TeamIds
    );

    public record CreateUserRequest(
        string FirstName,
        string LastName,
        string Email,
        string Password,
        string Role
    );
}