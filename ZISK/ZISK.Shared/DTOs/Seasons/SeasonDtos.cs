using System.ComponentModel.DataAnnotations;

namespace ZISK.Shared.DTOs.Seasons;

public record SeasonDto(
    Guid Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    bool IsActive,
    DateTime CreatedAt
);

public record CreateSeasonRequest(
    [property: Required(ErrorMessage = "Názov sezóny je povinný.")]
    [property: StringLength(100, MinimumLength = 2, ErrorMessage = "Názov musí mať 2–100 znakov.")]
    string Name,

    [property: Required(ErrorMessage = "Dátum začiatku je povinný.")]
    DateOnly StartDate,

    [property: Required(ErrorMessage = "Dátum konca je povinný.")]
    DateOnly EndDate
);

public record UpdateSeasonRequest(
    [property: Required(ErrorMessage = "Názov sezóny je povinný.")]
    [property: StringLength(100, MinimumLength = 2, ErrorMessage = "Názov musí mať 2–100 znakov.")]
    string Name,

    [property: Required(ErrorMessage = "Dátum začiatku je povinný.")]
    DateOnly StartDate,

    [property: Required(ErrorMessage = "Dátum konca je povinný.")]
    DateOnly EndDate
);
