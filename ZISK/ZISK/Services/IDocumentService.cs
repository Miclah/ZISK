using System.Security.Claims;
using ZISK.Shared.DTOs.Documents;
using DocumentCategory = ZISK.Shared.Enums.DocumentCategory;

namespace ZISK.Services;

public interface IDocumentService
{
    Task<List<DocumentDto>> GetDocumentsAsync(DocumentCategory? category);
    Task<DocumentDto> GetDocumentAsync(Guid id);
    Task<DocumentDto> CreateDocumentAsync(CreateDocumentRequest request);
    Task<string> UploadFileAsync(Guid id, IFormFile file);
    Task UpdateDocumentAsync(Guid id, UpdateDocumentRequest request);
    Task DeleteDocumentAsync(Guid id);
    Task<(string FullPath, string ContentType, string FileName)> GetDocumentFileAsync(Guid id);
}
