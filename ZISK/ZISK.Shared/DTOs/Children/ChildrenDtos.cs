using System.ComponentModel.DataAnnotations;

namespace ZISK.Shared.DTOs.Children;

public record CreateChildRequest(
    [Required(ErrorMessage = "Meno je povinné.")][MaxLength(100, ErrorMessage = "Meno môže mať max 100 znakov.")] string FirstName,
    [Required(ErrorMessage = "Priezvisko je povinné.")][MaxLength(100, ErrorMessage = "Priezvisko môže mať max 100 znakov.")] string LastName,
    [Required(ErrorMessage = "Dátum narodenia je povinný.")] DateOnly DateOfBirth,
    string? Email,
    bool SameAddressAsParent,
    [MaxLength(300, ErrorMessage = "Bydlisko môže mať max 300 znakov.")] string? Bydlisko,
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
    [Required(ErrorMessage = "Meno je povinné.")][MaxLength(100, ErrorMessage = "Meno môže mať max 100 znakov.")] string FirstName,
    [Required(ErrorMessage = "Priezvisko je povinné.")][MaxLength(100, ErrorMessage = "Priezvisko môže mať max 100 znakov.")] string LastName,
    [MaxLength(300, ErrorMessage = "Bydlisko môže mať max 300 znakov.")] string? Bydlisko,
    [Required(ErrorMessage = "Dátum narodenia je povinný.")] DateOnly DateOfBirth,
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
