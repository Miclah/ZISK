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
    [Required(ErrorMessage = "Názov sezóny je povinný.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Názov musí mať 2–100 znakov.")]
    string Name,

    [Required(ErrorMessage = "Dátum začiatku je povinný.")]
    DateOnly StartDate,

    [Required(ErrorMessage = "Dátum konca je povinný.")]
    DateOnly EndDate
);

public record UpdateSeasonRequest(
    [Required(ErrorMessage = "Názov sezóny je povinný.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Názov musí mať 2–100 znakov.")]
    string Name,

    [Required(ErrorMessage = "Dátum začiatku je povinný.")]
    DateOnly StartDate,

    [Required(ErrorMessage = "Dátum konca je povinný.")]
    DateOnly EndDate
);
