using System.ComponentModel.DataAnnotations;

namespace ZISK.Shared.DTOs.Excuses;

public record CreateExcuseRequest(
    [Required(ErrorMessage = "Dieťa je povinné.")]
    string ChildId,

    Guid? TrainingEventId,
    DateTime? DateFrom,
    DateTime? DateTo,

    [StringLength(1000, ErrorMessage = "Dôvod môže mať max 1000 znakov.")]
    string? Reason,

    [StringLength(500, ErrorMessage = "Poznámka môže mať max 500 znakov.")]
    string? Note
);
