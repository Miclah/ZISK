using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZISK.Services;
using ZISK.Shared.DTOs.Documents;
using ZISK.Shared.Localization;
using DocumentCategory = ZISK.Shared.Enums.DocumentCategory;

namespace ZISK.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ICurrentLanguage _currentLanguage;

    public DocumentsController(IDocumentService documentService, IWebHostEnvironment environment, IConfiguration configuration, ICurrentLanguage currentLanguage)
    {
        _documentService = documentService;
        _environment = environment;
        _configuration = configuration;
        _currentLanguage = currentLanguage;
    }

    [HttpGet]
    public async Task<ActionResult<List<DocumentDto>>> GetDocuments([FromQuery] DocumentCategory? category = null)
    {
        var result = await _documentService.GetDocumentsAsync(category, User);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DocumentDto>> GetDocument(Guid id)
    {
        try
        {
            var result = await _documentService.GetDocumentAsync(id, User);
            return Ok(result);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<DocumentDto>> CreateDocument([FromBody] CreateDocumentRequest request)
    {
        var result = await _documentService.CreateDocumentAsync(request);
        return CreatedAtAction(nameof(GetDocument), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/upload")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadFile(Guid id, IFormFile file)
    {
        // No client page reaches this today, but it's directly callable via the API - see
        // AnnouncementsController.UploadAttachment for the same reasoning.
        if (SeedModeResolver.Resolve(_configuration) == SeedMode.Demo)
            return Forbid();

        try
        {
            var filePath = await _documentService.UploadFileAsync(id, file);
            return Ok(new { filePath });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateDocument(Guid id, [FromBody] UpdateDocumentRequest request)
    {
        try
        {
            await _documentService.UpdateDocumentAsync(id, request);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteDocument(Guid id)
    {
        try
        {
            await _documentService.DeleteDocumentAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> DownloadDocument(Guid id)
    {
        try
        {
            var (relativePath, contentType, fileName) = await _documentService.GetDocumentFileAsync(id, User);
            var fullPath = Path.Combine(_environment.WebRootPath ?? "wwwroot", relativePath.TrimStart('/'));
            if (!System.IO.File.Exists(fullPath))
                return NotFound(Translations.Get(_currentLanguage.Current, "errors.file.notFound"));
            return PhysicalFile(fullPath, contentType, fileName);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
