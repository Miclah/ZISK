using System.ComponentModel.DataAnnotations;

namespace ZISK.Shared.DTOs.Excuses;

public record CreateExcuseRequest(
    [Required(ErrorMessage = "validation.child.required")]
    string ChildId,

    Guid? TrainingEventId,
    DateTime? DateFrom,
    DateTime? DateTo,

    [StringLength(1000, ErrorMessage = "validation.reason.maxLength1000")]
    string? Reason,

    [StringLength(500, ErrorMessage = "validation.note.maxLength500")]
    string? Note
);
