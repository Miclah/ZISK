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
    Guid Id,
    string FirstName,
    string LastName,
    string? Email,
    DateOnly DateOfBirth,
    List<string> ParentContacts
);

public record CreateTeamRequest(
    [property: Required(ErrorMessage = "Názov tímu je povinný.")]
    [property: StringLength(100, MinimumLength = 2, ErrorMessage = "Názov musí mať 2 – 100 znakov.")]
    string Name,

    [property: StringLength(50, ErrorMessage = "Skratka môže mať max 50 znakov.")]
    string? ShortName,

    [property: StringLength(500, ErrorMessage = "Popis môže mať max 500 znakov.")]
    string? Description
);

public record UpdateTeamRequest(
    [property: Required(ErrorMessage = "Názov tímu je povinný.")]
    [property: StringLength(100, MinimumLength = 2, ErrorMessage = "Názov musí mať 2 – 100 znakov.")]
    string Name,

    [property: StringLength(50, ErrorMessage = "Skratka môže mať max 50 znakov.")]
    string? ShortName,

    [property: StringLength(500, ErrorMessage = "Popis môže mať max 500 znakov.")]
    string? Description,

    bool IsActive
);
