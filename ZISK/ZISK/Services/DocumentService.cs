using Microsoft.EntityFrameworkCore;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Shared.DTOs.Documents;
using DocumentCategory = ZISK.Shared.Enums.DocumentCategory;

namespace ZISK.Services;

public class DocumentService : IDocumentService
{
    private readonly ApplicationDbContext _context;
    private readonly IFileService _fileService;

    public DocumentService(ApplicationDbContext context, IFileService fileService)
    {
        _context = context;
        _fileService = fileService;
    }

    public async Task<List<DocumentDto>> GetDocumentsAsync(DocumentCategory? category)
    {
        var query = _context.Documents.AsNoTracking();

        if (category.HasValue)
        {
            var dbCategory = (Data.Entities.DocumentCategory)(int)category.Value;
            query = query.Where(d => d.Category == dbCategory);
        }

        return await query
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new DocumentDto(
                d.Id, d.Title, d.FilePath,
                (DocumentCategory)(int)d.Category,
                d.TargetRoleId, d.UploadedAt))
            .ToListAsync();
    }

    public async Task<DocumentDto> GetDocumentAsync(Guid id)
    {
        var document = await _context.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new KeyNotFoundException();

        return new DocumentDto(document.Id, document.Title, document.FilePath,
            (DocumentCategory)(int)document.Category, document.TargetRoleId, document.UploadedAt);
    }

    public async Task<DocumentDto> CreateDocumentAsync(CreateDocumentRequest request)
    {
        var document = new Document
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            FilePath = string.Empty,
            Category = (Data.Entities.DocumentCategory)(int)request.Category,
            TargetRoleId = request.TargetRoleId,
            UploadedAt = DateTime.UtcNow
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        return new DocumentDto(document.Id, document.Title, document.FilePath,
            (DocumentCategory)(int)document.Category, document.TargetRoleId, document.UploadedAt);
    }

    public async Task<string> UploadFileAsync(Guid id, IFormFile file)
    {
        var document = await _context.Documents.FindAsync(id)
            ?? throw new KeyNotFoundException();

        var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".png" };
        var (isValid, errorMessage) = _fileService.ValidateFile(file, 10 * 1024 * 1024, allowedExtensions);
        if (!isValid)
            throw new ArgumentException(errorMessage);

        var relativePath = await _fileService.SaveFileAsync(file, "documents");
        document.FilePath = relativePath;
        await _context.SaveChangesAsync();

        return relativePath;
    }

    public async Task UpdateDocumentAsync(Guid id, UpdateDocumentRequest request)
    {
        var document = await _context.Documents.FindAsync(id)
            ?? throw new KeyNotFoundException();

        document.Title = request.Title;
        document.Category = (Data.Entities.DocumentCategory)(int)request.Category;
        document.TargetRoleId = request.TargetRoleId;

        await _context.SaveChangesAsync();
    }

    public async Task DeleteDocumentAsync(Guid id)
    {
        var document = await _context.Documents.FindAsync(id)
            ?? throw new KeyNotFoundException();

        _fileService.DeleteFile(document.FilePath);

        _context.Documents.Remove(document);
        await _context.SaveChangesAsync();
    }

    public async Task<(string FullPath, string ContentType, string FileName)> GetDocumentFileAsync(Guid id)
    {
        var document = await _context.Documents.FindAsync(id)
            ?? throw new KeyNotFoundException();

        if (string.IsNullOrEmpty(document.FilePath))
            throw new InvalidOperationException("Dokument nemá priradený súbor");

        var contentType = _fileService.GetContentType(document.FilePath);
        var fileName = Path.GetFileName(document.FilePath);

        return (document.FilePath, contentType, fileName);
    }
}
