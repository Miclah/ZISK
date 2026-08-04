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
    [Required(ErrorMessage = "validation.name.required")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "validation.name.length2to200")]
    string Title,

    DocumentCategory Category,
    string? TargetRoleId
);

public record UpdateDocumentRequest(
    [Required(ErrorMessage = "validation.name.required")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "validation.name.length2to200")]
    string Title,

    DocumentCategory Category,
    string? TargetRoleId
);
