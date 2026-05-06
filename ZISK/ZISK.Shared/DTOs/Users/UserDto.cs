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
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Meno musí mať 2 – 100 znakov.")]
        string? FirstName,

        [StringLength(100, MinimumLength = 2, ErrorMessage = "Priezvisko musí mať 2 – 100 znakov.")]
        string? LastName,

        string? Role,
        bool? IsActive,
        List<Guid>? TeamIds,

        [Phone(ErrorMessage = "Neplatný formát telefónneho čísla.")]
        string? PhoneNumber,

        [StringLength(20, ErrorMessage = "Rodné číslo môže mať max 20 znakov.")]
        string? RodneCislo,

        [StringLength(300, ErrorMessage = "Bydlisko môže mať max 300 znakov.")]
        string? Bydlisko,

        DateOnly? DateOfBirth,
        List<string>? ParentIds
    );

    public record CreateUserRequest(
        [Required(ErrorMessage = "Meno je povinné.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Meno musí mať 2 – 100 znakov.")]
        string FirstName,

        [Required(ErrorMessage = "Priezvisko je povinné.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Priezvisko musí mať 2 – 100 znakov.")]
        string LastName,

        [Required(ErrorMessage = "Email je povinný.")]
        [EmailAddress(ErrorMessage = "Neplatný formát emailu.")]
        string Email,

        [Required(ErrorMessage = "Heslo je povinné.")]
        [StringLength(100, MinimumLength = 4, ErrorMessage = "Heslo musí mať aspoň 4 znaky.")]
        string Password,

        [Required(ErrorMessage = "Rola je povinná.")]
        string Role,

        [Phone(ErrorMessage = "Neplatný formát telefónneho čísla.")]
        string? PhoneNumber,

        [StringLength(20, ErrorMessage = "Rodné číslo môže mať max 20 znakov.")]
        string? RodneCislo,

        [StringLength(300, ErrorMessage = "Bydlisko môže mať max 300 znakov.")]
        string? Bydlisko,

        DateOnly? DateOfBirth,
        List<string>? ParentIds
    );

    public record ParentOptionDto(
        string Id,
        string FullName
    );

    public record MyProfileDto(
        string Id,
        string FirstName,
        string LastName,
        string Email,
        string? PhoneNumber,
        string? Bydlisko,
        string? RodneCislo,
        DateOnly? DateOfBirth,
        string Role,
        DateTime CreatedAt
    );

    public record UpdateMyContactRequest(
        [Phone(ErrorMessage = "Neplatný formát telefónneho čísla.")]
        string? PhoneNumber,

        [StringLength(300, ErrorMessage = "Bydlisko môže mať max 300 znakov.")]
        string? Bydlisko
    );

    public record ChangeMyPasswordRequest(
        [Required(ErrorMessage = "Súčasné heslo je povinné.")]
        string CurrentPassword,

        [Required(ErrorMessage = "Nové heslo je povinné.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Heslo musí mať aspoň 8 znakov.")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$", // lookaheads: lowercase required, uppercase required, digit required
            ErrorMessage = "Heslo musí obsahovať aspoň jedno malé písmeno, veľké písmeno a číslicu.")]
        string NewPassword
    );

    public record ChangeMyEmailRequest(
        [Required(ErrorMessage = "Nový email je povinný.")]
        [EmailAddress(ErrorMessage = "Neplatný formát emailu.")]
        string NewEmail,

        [Required(ErrorMessage = "Súčasné heslo je povinné.")]
        string CurrentPassword
    );

    public record DeleteMyAccountRequest(
        [Required(ErrorMessage = "Súčasné heslo je povinné.")]
        string CurrentPassword
    );
}