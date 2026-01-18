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

    public AnnouncementsController(ApplicationDbContext context)
    {
        _context = context;
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
}
