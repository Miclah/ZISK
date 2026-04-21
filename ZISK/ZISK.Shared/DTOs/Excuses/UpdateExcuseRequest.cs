using System.ComponentModel.DataAnnotations;

namespace ZISK.Shared.DTOs.Excuses;

public record UpdateExcuseRequest(
    DateTime? DateFrom,
    DateTime? DateTo,

    [property: Required(ErrorMessage = "Dôvod je povinný.")]
    [property: StringLength(1000, MinimumLength = 3, ErrorMessage = "Dôvod musí mať 3 – 1000 znakov.")]
    string Reason,

    [property: StringLength(500, ErrorMessage = "Poznámka môže mať max 500 znakov.")]
    string? Note
);
