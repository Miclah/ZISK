using System.ComponentModel.DataAnnotations;
using ZISK.Shared.Enums;

namespace ZISK.Shared.DTOs.Documents;

public record DocumentDto(
    Guid Id,
    string Title,
    string FilePath,
    DocumentCategory Category,
    string? TargetRoleName,
    DateTime UploadedAt
);

public record CreateDocumentRequest(
    [Required(ErrorMessage = "Názov je povinný.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "Názov musí mať 2 – 200 znakov.")]
    string Title,

    DocumentCategory Category,
    string? TargetRoleId
);

public record UpdateDocumentRequest(
    [Required(ErrorMessage = "Názov je povinný.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "Názov musí mať 2 – 200 znakov.")]
    string Title,

    DocumentCategory Category,
    string? TargetRoleId
);
