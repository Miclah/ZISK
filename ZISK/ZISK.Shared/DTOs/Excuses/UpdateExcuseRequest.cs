using System.ComponentModel.DataAnnotations;

namespace ZISK.Shared.DTOs.Excuses;

public record UpdateExcuseRequest(
    DateTime? DateFrom,
    DateTime? DateTo,

    [Required(ErrorMessage = "Dôvod je povinný.")]
    [StringLength(1000, MinimumLength = 3, ErrorMessage = "Dôvod musí mať 3 – 1000 znakov.")]
    string Reason,

    [StringLength(500, ErrorMessage = "Poznámka môže mať max 500 znakov.")]
    string? Note
);
