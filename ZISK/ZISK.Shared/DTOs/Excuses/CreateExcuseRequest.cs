using System.ComponentModel.DataAnnotations;

namespace ZISK.Shared.DTOs.Excuses;

public record CreateExcuseRequest(
    [property: Required(ErrorMessage = "Dieťa je povinné.")]
    Guid ChildId,

    Guid? TrainingEventId,
    DateTime? DateFrom,
    DateTime? DateTo,

    [property: StringLength(1000, ErrorMessage = "Dôvod môže mať max 1000 znakov.")]
    string? Reason,

    [property: StringLength(500, ErrorMessage = "Poznámka môže mať max 500 znakov.")]
    string? Note
);