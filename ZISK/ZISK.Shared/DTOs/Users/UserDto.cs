using System.ComponentModel.DataAnnotations;

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
        [property: StringLength(100, MinimumLength = 2, ErrorMessage = "Meno musí mať 2 – 100 znakov.")]
        string? FirstName,

        [property: StringLength(100, MinimumLength = 2, ErrorMessage = "Priezvisko musí mať 2 – 100 znakov.")]
        string? LastName,

        string? Role,
        bool? IsActive,
        List<Guid>? TeamIds,

        [property: Phone(ErrorMessage = "Neplatný formát telefónneho čísla.")]
        string? PhoneNumber,

        [property: StringLength(20, ErrorMessage = "Rodné číslo môže mať max 20 znakov.")]
        string? RodneCislo,

        [property: StringLength(300, ErrorMessage = "Bydlisko môže mať max 300 znakov.")]
        string? Bydlisko,

        DateOnly? DateOfBirth,
        List<string>? ParentIds
    );

    public record CreateUserRequest(
        [property: Required(ErrorMessage = "Meno je povinné.")]
        [property: StringLength(100, MinimumLength = 2, ErrorMessage = "Meno musí mať 2 – 100 znakov.")]
        string FirstName,

        [property: Required(ErrorMessage = "Priezvisko je povinné.")]
        [property: StringLength(100, MinimumLength = 2, ErrorMessage = "Priezvisko musí mať 2 – 100 znakov.")]
        string LastName,

        [property: Required(ErrorMessage = "Email je povinný.")]
        [property: EmailAddress(ErrorMessage = "Neplatný formát emailu.")]
        string Email,

        [property: Required(ErrorMessage = "Heslo je povinné.")]
        [property: StringLength(100, MinimumLength = 4, ErrorMessage = "Heslo musí mať aspoň 4 znaky.")]
        string Password,

        [property: Required(ErrorMessage = "Rola je povinná.")]
        string Role,

        [property: Phone(ErrorMessage = "Neplatný formát telefónneho čísla.")]
        string? PhoneNumber,

        [property: StringLength(20, ErrorMessage = "Rodné číslo môže mať max 20 znakov.")]
        string? RodneCislo,

        [property: StringLength(300, ErrorMessage = "Bydlisko môže mať max 300 znakov.")]
        string? Bydlisko,

        DateOnly? DateOfBirth,
        List<string>? ParentIds
    );

    public record ParentOptionDto(
        string Id,
        string FullName
    );
}