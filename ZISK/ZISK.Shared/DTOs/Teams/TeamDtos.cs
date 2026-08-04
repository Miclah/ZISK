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
    [Required(ErrorMessage = "validation.teamName.required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "validation.name.length2to100")]
    string Name,

    [StringLength(50, ErrorMessage = "validation.shortName.maxLength50")]
    string? ShortName,

    [StringLength(500, ErrorMessage = "validation.description.maxLength500")]
    string? Description
);

public record UpdateTeamRequest(
    [Required(ErrorMessage = "validation.teamName.required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "validation.name.length2to100")]
    string Name,

    [StringLength(50, ErrorMessage = "validation.shortName.maxLength50")]
    string? ShortName,

    [StringLength(500, ErrorMessage = "validation.description.maxLength500")]
    string? Description,

    bool IsActive
);
