using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZISK.Services;
using ZISK.Shared.DTOs.Announcements;
using ZISK.Shared.Localization;
using TargetAudience = ZISK.Shared.Enums.TargetAudience;

namespace ZISK.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AnnouncementsController : ControllerBase
{
    private readonly IAnnouncementService _announcementService;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ICurrentLanguage _currentLanguage;

    public AnnouncementsController(IAnnouncementService announcementService, IWebHostEnvironment environment, IConfiguration configuration, ICurrentLanguage currentLanguage)
    {
        _announcementService = announcementService;
        _environment = environment;
        _configuration = configuration;
        _currentLanguage = currentLanguage;
    }

    [HttpGet]
    public async Task<ActionResult<List<AnnouncementListDto>>> GetAnnouncements(
        [FromQuery] Guid? teamId = null,
        [FromQuery] TargetAudience? audience = null)
    {
        var result = await _announcementService.GetAnnouncementsAsync(teamId, audience, User);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AnnouncementDto>> GetAnnouncement(Guid id)
    {
        try
        {
            var result = await _announcementService.GetAnnouncementAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<ActionResult<AnnouncementDto>> CreateAnnouncement([FromBody] CreateAnnouncementRequest request)
    {
        try
        {
            var result = await _announcementService.CreateAnnouncementAsync(request, User);
            return CreatedAtAction(nameof(GetAnnouncement), new { id = result.Id }, result);
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (UnauthorizedAccessException) { return Unauthorized(); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<IActionResult> UpdateAnnouncement(Guid id, [FromBody] UpdateAnnouncementRequest request)
    {
        try
        {
            await _announcementService.UpdateAnnouncementAsync(id, request, User);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<IActionResult> DeleteAnnouncement(Guid id)
    {
        try
        {
            await _announcementService.DeleteAnnouncementAsync(id, User);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("unread/count")]
    public async Task<ActionResult<int>> GetUnreadCount()
    {
        var count = await _announcementService.GetUnreadCountAsync();
        return Ok(count);
    }

    [HttpPost("{id:guid}/attachments")]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<ActionResult<AttachmentDto>> UploadAttachment(Guid id, IFormFile file)
    {
        // Belt-and-suspenders: AnnouncementsCreate.razor already hides the file picker in demo
        // mode, but this endpoint is directly callable, and cloned sessions sharing a physical
        // file path is a real (if narrow) hazard - see DemoSessionService's cloning notes.
        if (SeedModeResolver.Resolve(_configuration) == SeedMode.Demo)
            return Forbid();

        try
        {
            var result = await _announcementService.UploadAttachmentAsync(id, file);
            return Ok(result);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("{announcementId:guid}/attachments/{attachmentId:guid}/download")]
    public async Task<IActionResult> DownloadAttachment(Guid announcementId, Guid attachmentId)
    {
        try
        {
            var (relativePath, contentType, fileName) = await _announcementService.GetAttachmentFileAsync(announcementId, attachmentId);
            var fullPath = Path.Combine(_environment.WebRootPath ?? "wwwroot", relativePath.TrimStart('/'));
            if (!System.IO.File.Exists(fullPath))
                return NotFound(Translations.Get(_currentLanguage.Current, "errors.file.notFound"));
            return PhysicalFile(fullPath, contentType, fileName);
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpDelete("{announcementId:guid}/attachments/{attachmentId:guid}")]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<IActionResult> DeleteAttachment(Guid announcementId, Guid attachmentId)
    {
        try
        {
            await _announcementService.DeleteAttachmentAsync(announcementId, attachmentId);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
