using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Shared.DTOs.Announcements;
using AnnouncementPriority = ZISK.Shared.Enums.AnnouncementPriority;
using TargetAudience = ZISK.Shared.Enums.TargetAudience;

namespace ZISK.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AnnouncementsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public AnnouncementsController(ApplicationDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    [HttpGet]
    public async Task<ActionResult<List<AnnouncementListDto>>> GetAnnouncements(
        [FromQuery] Guid? teamId = null,
        [FromQuery] TargetAudience? audience = null)
    {
        var query = _context.Announcements
            .Include(a => a.AuthorUser)
            .Include(a => a.TargetTeam)
            .Include(a => a.Attachments)
            .AsNoTracking();

        if (teamId.HasValue)
        {
            query = query.Where(a => a.TargetTeamId == null || a.TargetTeamId == teamId.Value);
        }

        if (audience.HasValue)
        {
            var dbAudience = (Data.Entities.TargetAudience)(int)audience.Value;
            query = query.Where(a => a.TargetAudience == Data.Entities.TargetAudience.All || a.TargetAudience == dbAudience);
        }

        query = query.Where(a => a.ValidUntil == null || a.ValidUntil >= DateTime.UtcNow);

        var announcements = await query
            .OrderByDescending(a => a.IsPinned)
            .ThenByDescending(a => a.PublishDate)
            .Select(a => new AnnouncementListDto(
                a.Id,
                a.Title,
                a.Content.Length > 200 ? a.Content.Substring(0, 200) + "..." : a.Content,
                a.TargetTeam != null ? a.TargetTeam.Name : null,
                (TargetAudience)(int)a.TargetAudience,
                (AnnouncementPriority)(int)a.Priority,
                a.IsPinned,
                a.ValidUntil,
                $"{a.AuthorUser.FirstName} {a.AuthorUser.LastName}",
                a.PublishDate,
                a.Attachments.Count,
                false // TODO: Implementovať sledovanie prečítaných oznamov
            ))
            .ToListAsync();

        return Ok(announcements);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AnnouncementDto>> GetAnnouncement(Guid id)
    {
        var announcement = await _context.Announcements
            .Include(a => a.AuthorUser)
            .Include(a => a.TargetTeam)
            .Include(a => a.Attachments)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);

        if (announcement == null)
            return NotFound();

        return Ok(new AnnouncementDto(
            announcement.Id,
            announcement.Title,
            announcement.Content,
            announcement.TargetTeamId,
            announcement.TargetTeam?.Name,
            (TargetAudience)(int)announcement.TargetAudience,
            (AnnouncementPriority)(int)announcement.Priority,
            announcement.IsPinned,
            announcement.ValidUntil,
            announcement.AuthorUserId,
            $"{announcement.AuthorUser.FirstName} {announcement.AuthorUser.LastName}",
            announcement.PublishDate,
            announcement.UpdatedAt,
            0, // TODO: Implementovať počítadlo zobrazení
            false, // TODO: Implementovať sledovanie prečítaných oznamov
            announcement.Attachments.Select(att => new AttachmentDto(
                att.Id,
                att.FileName,
                att.ContentType,
                att.FileSize
            )).ToList()
        ));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<ActionResult<AnnouncementDto>> CreateAnnouncement([FromBody] CreateAnnouncementRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length < 3 || request.Title.Length > 200)
            return BadRequest("Nadpis musí mať 3-200 znakov");
        
        if (string.IsNullOrWhiteSpace(request.Content) || request.Content.Length < 10)
            return BadRequest("Obsah musí mať minimálne 10 znakov");

        if (request.TargetTeamId.HasValue && !await _context.Teams.AnyAsync(t => t.Id == request.TargetTeamId.Value))
            return BadRequest("Neplatný tím");

        if (request.ValidUntil.HasValue && request.ValidUntil.Value < DateTime.UtcNow)
            return BadRequest("Dátum platnosti nemôže byť v minulosti");

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return Unauthorized();

        var announcement = new Announcement
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Content = request.Content,
            TargetTeamId = request.TargetTeamId,
            TargetAudience = (Data.Entities.TargetAudience)(int)request.TargetAudience,
            Priority = (Data.Entities.AnnouncementPriority)(int)request.Priority,
            IsPinned = request.IsPinned,
            ValidUntil = request.ValidUntil,
            AuthorUserId = userId,
            PublishDate = DateTime.UtcNow
        };

        _context.Announcements.Add(announcement);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAnnouncement), new { id = announcement.Id }, new AnnouncementDto(
            announcement.Id,
            announcement.Title,
            announcement.Content,
            announcement.TargetTeamId,
            null,
            (TargetAudience)(int)announcement.TargetAudience,
            (AnnouncementPriority)(int)announcement.Priority,
            announcement.IsPinned,
            announcement.ValidUntil,
            announcement.AuthorUserId,
            $"{user.FirstName} {user.LastName}",
            announcement.PublishDate,
            null,
            0,
            false,
            new List<AttachmentDto>()
        ));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<IActionResult> UpdateAnnouncement(Guid id, [FromBody] UpdateAnnouncementRequest request)
    {
        // Server-side validácia
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length < 3 || request.Title.Length > 200)
            return BadRequest("Nadpis musí mať 3-200 znakov");
        
        if (string.IsNullOrWhiteSpace(request.Content) || request.Content.Length < 10)
            return BadRequest("Obsah musí mať minimálne 10 znakov");

        if (request.TargetTeamId.HasValue && !await _context.Teams.AnyAsync(t => t.Id == request.TargetTeamId.Value))
            return BadRequest("Neplatný tím");

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var announcement = await _context.Announcements.FindAsync(id);
        
        if (announcement == null)
            return NotFound();

        if (announcement.AuthorUserId != userId && !User.IsInRole("Admin"))
            return Forbid();

        announcement.Title = request.Title;
        announcement.Content = request.Content;
        announcement.TargetTeamId = request.TargetTeamId;
        announcement.TargetAudience = (Data.Entities.TargetAudience)(int)request.TargetAudience;
        announcement.Priority = (Data.Entities.AnnouncementPriority)(int)request.Priority;
        announcement.IsPinned = request.IsPinned;
        announcement.ValidUntil = request.ValidUntil;
        announcement.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<IActionResult> DeleteAnnouncement(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var announcement = await _context.Announcements
            .Include(a => a.Attachments)
            .FirstOrDefaultAsync(a => a.Id == id);
        
        if (announcement == null)
            return NotFound();

        if (announcement.AuthorUserId != userId && !User.IsInRole("Admin"))
            return Forbid();

        _context.AnnouncementAttachments.RemoveRange(announcement.Attachments);
        _context.Announcements.Remove(announcement);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("unread/count")]
    public async Task<ActionResult<int>> GetUnreadCount()
    {
        // TODO: Implementovať správne sledovanie prečítaných oznamov
        // zatial poslednych 7 dni
        var count = await _context.Announcements
            .CountAsync(a => a.PublishDate >= DateTime.UtcNow.AddDays(-7));

        return Ok(count);
    }

    [HttpPost("{id:guid}/attachments")]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<ActionResult<AttachmentDto>> UploadAttachment(Guid id, IFormFile file)
    {
        var announcement = await _context.Announcements.FindAsync(id);
        if (announcement == null)
            return NotFound();

        if (file == null || file.Length == 0)
            return BadRequest("Súbor je prázdny");

        var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".jpeg", ".png", ".gif" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        
        if (!allowedExtensions.Contains(extension))
            return BadRequest("Nepodporovaný typ súboru");

        // Max 10MB
        if (file.Length > 10 * 1024 * 1024)
            return BadRequest("Súbor je príliš veľký (max 10MB)");

        var uploadsFolder = Path.Combine(_environment.WebRootPath ?? "wwwroot", "uploads", "attachments");
        Directory.CreateDirectory(uploadsFolder);

        var uniqueFileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var attachment = new AnnouncementAttachment
        {
            Id = Guid.NewGuid(),
            AnnouncementId = id,
            FileName = file.FileName,
            FilePath = $"/uploads/attachments/{uniqueFileName}",
            ContentType = file.ContentType,
            FileSize = file.Length,
            UploadedAt = DateTime.UtcNow
        };

        _context.AnnouncementAttachments.Add(attachment);
        await _context.SaveChangesAsync();

        return Ok(new AttachmentDto(
            attachment.Id,
            attachment.FileName,
            attachment.ContentType,
            attachment.FileSize
        ));
    }

    [HttpGet("{announcementId:guid}/attachments/{attachmentId:guid}/download")]
    public async Task<IActionResult> DownloadAttachment(Guid announcementId, Guid attachmentId)
    {
        var attachment = await _context.AnnouncementAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.AnnouncementId == announcementId);

        if (attachment == null)
            return NotFound();

        var fullPath = Path.Combine(_environment.WebRootPath ?? "wwwroot", attachment.FilePath.TrimStart('/'));
        if (!System.IO.File.Exists(fullPath))
            return NotFound("Súbor neexistuje");

        return PhysicalFile(fullPath, attachment.ContentType ?? "application/octet-stream", attachment.FileName);
    }

    [HttpDelete("{announcementId:guid}/attachments/{attachmentId:guid}")]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<IActionResult> DeleteAttachment(Guid announcementId, Guid attachmentId)
    {
        var attachment = await _context.AnnouncementAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.AnnouncementId == announcementId);

        if (attachment == null)
            return NotFound();

        if (!string.IsNullOrEmpty(attachment.FilePath))
        {
            var fullPath = Path.Combine(_environment.WebRootPath ?? "wwwroot", attachment.FilePath.TrimStart('/'));
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }

        _context.AnnouncementAttachments.Remove(attachment);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
