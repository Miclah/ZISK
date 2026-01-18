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
    DateOnly DateOfBirth
);

public record CreateTeamRequest(
    string Name,
    string? ShortName,
    string? Description
);

public record UpdateTeamRequest(
    string Name,
    string? ShortName,
    string? Description,
    bool IsActive
);
