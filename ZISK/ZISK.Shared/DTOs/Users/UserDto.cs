namespace ZISK.Shared.DTOs.Users
{
    public record UserDto(
        string Id,
        string FirstName,
        string LastName,
        string Email,
        string? PhoneNumber,
        string Role,
        bool IsActive,
        DateTime CreatedAt,
        string? RodneCislo,
        string? Bydlisko,
        DateOnly? DateOfBirth,
        List<UserTeamDto> Teams,
        List<ParentOptionDto> Parents
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
        string? PhoneNumber,
        string Role,
        bool IsActive,
        DateTime CreatedAt,
        string? TeamName,
        List<UserTeamDto> Teams
    );

    public record UpdateUserRequest(
        string? FirstName,
        string? LastName,
        string? Role,
        bool? IsActive,
        List<Guid>? TeamIds,
        string? PhoneNumber,
        string? RodneCislo,
        string? Bydlisko,
        DateOnly? DateOfBirth,
        List<string>? ParentIds
    );

    public record CreateUserRequest(
        string FirstName,
        string LastName,
        string Email,
        string Password,
        string Role,
        string? PhoneNumber,
        string? RodneCislo,
        string? Bydlisko,
        DateOnly? DateOfBirth,
        List<string>? ParentIds
    );

    public record ParentOptionDto(
        string Id,
        string FullName
    );
}