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
        [StringLength(100, MinimumLength = 2, ErrorMessage = "validation.firstName.length2to100")]
        string? FirstName,

        [StringLength(100, MinimumLength = 2, ErrorMessage = "validation.lastName.length2to100")]
        string? LastName,

        string? Role,
        bool? IsActive,
        List<Guid>? TeamIds,

        [Phone(ErrorMessage = "validation.phone.invalidFormat")]
        string? PhoneNumber,

        [StringLength(20, ErrorMessage = "validation.rodneCislo.maxLength20")]
        string? RodneCislo,

        [StringLength(300, ErrorMessage = "validation.bydlisko.maxLength300")]
        string? Bydlisko,

        DateOnly? DateOfBirth,
        List<string>? ParentIds
    );

    public record CreateUserRequest(
        [Required(ErrorMessage = "validation.firstName.required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "validation.firstName.length2to100")]
        string FirstName,

        [Required(ErrorMessage = "validation.lastName.required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "validation.lastName.length2to100")]
        string LastName,

        [Required(ErrorMessage = "validation.email.required")]
        [EmailAddress(ErrorMessage = "validation.email.invalidFormat")]
        string Email,

        [Required(ErrorMessage = "validation.password.required")]
        [StringLength(100, MinimumLength = 4, ErrorMessage = "validation.password.minLength4")]
        string Password,

        [Required(ErrorMessage = "validation.role.required")]
        string Role,

        [Phone(ErrorMessage = "validation.phone.invalidFormat")]
        string? PhoneNumber,

        [StringLength(20, ErrorMessage = "validation.rodneCislo.maxLength20")]
        string? RodneCislo,

        [StringLength(300, ErrorMessage = "validation.bydlisko.maxLength300")]
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
        [Phone(ErrorMessage = "validation.phone.invalidFormat")]
        string? PhoneNumber,

        [StringLength(300, ErrorMessage = "validation.bydlisko.maxLength300")]
        string? Bydlisko
    );

    public record ChangeMyPasswordRequest(
        [Required(ErrorMessage = "validation.currentPassword.required")]
        string CurrentPassword,

        [Required(ErrorMessage = "validation.newPassword.required")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "validation.newPassword.minLength8")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$", // lookaheads: lowercase required, uppercase required, digit required
            ErrorMessage = "validation.newPassword.complexity")]
        string NewPassword
    );

    public record ChangeMyEmailRequest(
        [Required(ErrorMessage = "validation.newEmail.required")]
        [EmailAddress(ErrorMessage = "validation.email.invalidFormat")]
        string NewEmail,

        [Required(ErrorMessage = "validation.currentPassword.required")]
        string CurrentPassword
    );

    public record DeleteMyAccountRequest(
        [Required(ErrorMessage = "validation.currentPassword.required")]
        string CurrentPassword
    );
}