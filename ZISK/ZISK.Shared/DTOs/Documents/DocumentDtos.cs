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
    string Title,
    DocumentCategory Category,
    string? TargetRoleId
);

public record UpdateDocumentRequest(
    string Title,
    DocumentCategory Category,
    string? TargetRoleId
);
