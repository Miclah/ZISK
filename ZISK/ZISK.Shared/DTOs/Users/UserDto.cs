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
        List<UserTeamDto> Teams,
        string? PhoneNumber = null,
        string? RodneCislo = null,
        DateTime? DateOfBirth = null,
        string? Bydlisko = null
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
        bool IsActive,
        List<UserTeamDto> Teams
    );

    public record UpdateUserRequest(
        string? FirstName,
        string? LastName,
        string? Role,
        bool? IsActive,
        string? PhoneNumber,
        string? RodneCislo,
        DateTime? DateOfBirth,
        string? Bydlisko,
        List<Guid>? TeamIds
    );

    public record CreateUserRequest(
        string FirstName,
        string LastName,
        string Email,
        string Password,
        string Role,
        string? PhoneNumber = null,
        string? RodneCislo = null,
        DateTime? DateOfBirth = null,
        string? Bydlisko = null,
        string? ParentId = null
    );
}