using System.ComponentModel.DataAnnotations;

namespace ZISK.Shared.DTOs.Teams;

public record TeamDto(
    Guid Id,
    string Name,
    string? ShortName,
    string? Description,
    bool IsActive,
    int MemberCount
);

public record TeamDetailDto(
    Guid Id,
    string Name,
    string? ShortName,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    List<TeamMemberDto> Members
);

public record TeamMemberDto(
    string Id,
    string FirstName,
    string LastName,
    string? Email,
    DateOnly? DateOfBirth,
    List<string> ParentContacts
);

public record CreateTeamRequest(
    [Required(ErrorMessage = "Názov tímu je povinný.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Názov musí mať 2 – 100 znakov.")]
    string Name,

    [StringLength(50, ErrorMessage = "Skratka môže mať max 50 znakov.")]
    string? ShortName,

    [StringLength(500, ErrorMessage = "Popis môže mať max 500 znakov.")]
    string? Description
);

public record UpdateTeamRequest(
    [Required(ErrorMessage = "Názov tímu je povinný.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Názov musí mať 2 – 100 znakov.")]
    string Name,

    [StringLength(50, ErrorMessage = "Skratka môže mať max 50 znakov.")]
    string? ShortName,

    [StringLength(500, ErrorMessage = "Popis môže mať max 500 znakov.")]
    string? Description,

    bool IsActive
);
