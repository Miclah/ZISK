using Refit;
using ZISK.Shared.DTOs.Documents;
using ZISK.Shared.Enums;

namespace ZISK.Client.Services;

public interface IDocumentsApi
{
    [Get("/api/documents")]
    Task<List<DocumentDto>> GetDocumentsAsync([Query] DocumentCategory? category = null);

    [Get("/api/documents/{id}")]
    Task<DocumentDto> GetDocumentAsync(Guid id);

    [Post("/api/documents")]
    Task<DocumentDto> CreateDocumentAsync([Body] CreateDocumentRequest request);

    [Put("/api/documents/{id}")]
    Task UpdateDocumentAsync(Guid id, [Body] UpdateDocumentRequest request);

    [Delete("/api/documents/{id}")]
    Task DeleteDocumentAsync(Guid id);

}
