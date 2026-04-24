using System.ComponentModel.DataAnnotations;

namespace ZISK.Shared.DTOs.Children;

public record CreateChildRequest(
    [Required][MaxLength(100)] string FirstName,
    [Required][MaxLength(100)] string LastName,
    [Required] DateOnly DateOfBirth,
    string? Email,
    bool SameAddressAsParent,
    [MaxLength(300)] string? Bydlisko,
    Guid? TeamId,
    bool CreateCredentials,
    string? Password,
    string? OverrideUsername
);

public record ParentDto(
    string UserId,
    string FirstName,
    string LastName,
    string Email,
    DateTime JoinedAt,
    bool IsPrimary
);

public record UpdateChildRequest(
    [Required][MaxLength(100)] string FirstName,
    [Required][MaxLength(100)] string LastName,
    [MaxLength(300)] string? Bydlisko,
    [Required] DateOnly DateOfBirth,
    Guid? TeamId
);

public record ChildDetailDto(
    string Id,
    string FirstName,
    string LastName,
    DateOnly? DateOfBirth,
    string? Bydlisko,
    Guid? TeamId,
    string? TeamName
);
