using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Shared.DTOs.Documents;
using DocumentCategory = ZISK.Shared.Enums.DocumentCategory;

namespace ZISK.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public DocumentsController(ApplicationDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    [HttpGet]
    public async Task<ActionResult<List<DocumentDto>>> GetDocuments(
        [FromQuery] DocumentCategory? category = null)
    {
        var query = _context.Documents.AsNoTracking();

        if (category.HasValue)
        {
            var dbCategory = (Data.Entities.DocumentCategory)(int)category.Value;
            query = query.Where(d => d.Category == dbCategory);
        }

        // TODO: Filter podľa role používateľa

        var documents = await query
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new DocumentDto(
                d.Id,
                d.Title,
                d.FilePath,
                (DocumentCategory)(int)d.Category,
                d.TargetRoleId,
                d.UploadedAt
            ))
            .ToListAsync();

        return Ok(documents);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DocumentDto>> GetDocument(Guid id)
    {
        var document = await _context.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id);

        if (document == null)
            return NotFound();

        return Ok(new DocumentDto(
            document.Id,
            document.Title,
            document.FilePath,
            (DocumentCategory)(int)document.Category,
            document.TargetRoleId,
            document.UploadedAt
        ));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<DocumentDto>> CreateDocument([FromBody] CreateDocumentRequest request)
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

        return CreatedAtAction(nameof(GetDocument), new { id = document.Id }, new DocumentDto(
            document.Id,
            document.Title,
            document.FilePath,
            (DocumentCategory)(int)document.Category,
            document.TargetRoleId,
            document.UploadedAt
        ));
    }

    [HttpPost("{id:guid}/upload")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UploadFile(Guid id, IFormFile file)
    {
        var document = await _context.Documents.FindAsync(id);
        if (document == null)
            return NotFound();

        if (file == null || file.Length == 0)
            return BadRequest("Súbor je prázdny");

        var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".png" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        
        if (!allowedExtensions.Contains(extension))
            return BadRequest("Nepodporovaný typ súboru");

        var uploadsFolder = Path.Combine(_environment.WebRootPath ?? "wwwroot", "uploads", "documents");
        Directory.CreateDirectory(uploadsFolder);

        var uniqueFileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        document.FilePath = $"/uploads/documents/{uniqueFileName}";
        await _context.SaveChangesAsync();

        return Ok(new { filePath = document.FilePath });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateDocument(Guid id, [FromBody] UpdateDocumentRequest request)
    {
        var document = await _context.Documents.FindAsync(id);
        if (document == null)
            return NotFound();

        document.Title = request.Title;
        document.Category = (Data.Entities.DocumentCategory)(int)request.Category;
        document.TargetRoleId = request.TargetRoleId;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteDocument(Guid id)
    {
        var document = await _context.Documents.FindAsync(id);
        if (document == null)
            return NotFound();

        if (!string.IsNullOrEmpty(document.FilePath))
        {
            var fullPath = Path.Combine(_environment.WebRootPath ?? "wwwroot", document.FilePath.TrimStart('/'));
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }

        _context.Documents.Remove(document);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> DownloadDocument(Guid id)
    {
        var document = await _context.Documents.FindAsync(id);
        if (document == null)
            return NotFound();

        if (string.IsNullOrEmpty(document.FilePath))
            return BadRequest("Dokument nemá priradený súbor");

        var fullPath = Path.Combine(_environment.WebRootPath ?? "wwwroot", document.FilePath.TrimStart('/'));
        if (!System.IO.File.Exists(fullPath))
            return NotFound("Súbor neexistuje");

        var contentType = GetContentType(document.FilePath);
        var fileName = Path.GetFileName(document.FilePath);

        return PhysicalFile(fullPath, contentType, fileName);
    }

    private static string GetContentType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };
    }
}
