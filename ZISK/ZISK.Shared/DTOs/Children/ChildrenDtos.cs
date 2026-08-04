using System.ComponentModel.DataAnnotations;

namespace ZISK.Shared.DTOs.Children;

public record CreateChildRequest(
    [Required(ErrorMessage = "validation.firstName.required")][MaxLength(100, ErrorMessage = "validation.firstName.maxLength100")] string FirstName,
    [Required(ErrorMessage = "validation.lastName.required")][MaxLength(100, ErrorMessage = "validation.lastName.maxLength100")] string LastName,
    [Required(ErrorMessage = "validation.dateOfBirth.required")] DateOnly DateOfBirth,
    string? Email,
    bool SameAddressAsParent,
    [MaxLength(300, ErrorMessage = "validation.bydlisko.maxLength300")] string? Bydlisko,
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
    [Required(ErrorMessage = "validation.firstName.required")][MaxLength(100, ErrorMessage = "validation.firstName.maxLength100")] string FirstName,
    [Required(ErrorMessage = "validation.lastName.required")][MaxLength(100, ErrorMessage = "validation.lastName.maxLength100")] string LastName,
    [MaxLength(300, ErrorMessage = "validation.bydlisko.maxLength300")] string? Bydlisko,
    [Required(ErrorMessage = "validation.dateOfBirth.required")] DateOnly DateOfBirth,
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
