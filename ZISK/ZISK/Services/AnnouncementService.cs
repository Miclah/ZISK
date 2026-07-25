using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Extensions;
using ZISK.Shared.DTOs.Announcements;
using AnnouncementPriority = ZISK.Shared.Enums.AnnouncementPriority;
using TargetAudience = ZISK.Shared.Enums.TargetAudience;

namespace ZISK.Services;

public class AnnouncementService : IAnnouncementService
{
    private readonly ApplicationDbContext _context;
    private readonly ITeamAccessService _teamAccessService;
    private readonly IFileService _fileService;

    public AnnouncementService(ApplicationDbContext context, ITeamAccessService teamAccessService, IFileService fileService)
    {
        _context = context;
        _teamAccessService = teamAccessService;
        _fileService = fileService;
    }

    public async Task<List<AnnouncementListDto>> GetAnnouncementsAsync(Guid? teamId, TargetAudience? audience, ClaimsPrincipal user)
    {
        var query = _context.Announcements
            .Include(a => a.AuthorUser)
            .Include(a => a.TargetTeam)
            .Include(a => a.Attachments)
            .AsNoTracking();

        var accessibleTeamIds = await _teamAccessService.GetAccessibleTeamIdsAsync(user);
        if (accessibleTeamIds is not null)
            query = query.Where(a => a.TargetTeamId == null || (a.TargetTeamId.HasValue && accessibleTeamIds.Contains(a.TargetTeamId.Value)));

        if (teamId.HasValue)
            query = query.Where(a => a.TargetTeamId == null || a.TargetTeamId == teamId.Value);

        if (audience.HasValue)
        {
            var dbAudience = (Data.Entities.TargetAudience)(int)audience.Value;
            query = query.Where(a => a.TargetAudience == Data.Entities.TargetAudience.All || a.TargetAudience == dbAudience);
        }

        query = query.Where(a => a.ValidUntil == null || a.ValidUntil >= DateTime.UtcNow);

        return await query
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
                false // IsRead is not yet tracked per user — always returns false until read-tracking is implemented
            ))
            .ToListAsync();
    }

    public async Task<AnnouncementDto> GetAnnouncementAsync(Guid id)
    {
        var announcement = await _context.Announcements
            .Include(a => a.AuthorUser)
            .Include(a => a.TargetTeam)
            .Include(a => a.Attachments)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id)
            ?? throw new KeyNotFoundException();

        return MapToDto(announcement);
    }

    public async Task<AnnouncementDto> CreateAnnouncementAsync(CreateAnnouncementRequest request, ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length < 3 || request.Title.Length > 200)
            throw new ArgumentException("Nadpis musí mať 3-200 znakov");

        if (string.IsNullOrWhiteSpace(request.Content) || request.Content.Length < 10)
            throw new ArgumentException("Obsah musí mať minimálne 10 znakov");

        if (request.TargetTeamId.HasValue && !await _context.Teams.AnyAsync(t => t.Id == request.TargetTeamId.Value))
            throw new ArgumentException("Neplatný tím");

        if (request.ValidUntil.HasValue && request.ValidUntil.Value < DateTime.UtcNow)
            throw new ArgumentException("Dátum platnosti nemôže byť v minulosti");

        var userId = user.GetRequiredUserId();
        var authorUser = await _context.Users.FindAsync(userId)
            ?? throw new UnauthorizedAccessException();

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

        return new AnnouncementDto(
            announcement.Id, announcement.Title, announcement.Content,
            announcement.TargetTeamId, null,
            (TargetAudience)(int)announcement.TargetAudience,
            (AnnouncementPriority)(int)announcement.Priority,
            announcement.IsPinned, announcement.ValidUntil, announcement.AuthorUserId,
            $"{authorUser.FirstName} {authorUser.LastName}",
            announcement.PublishDate, null, 0, false, []
        );
    }

    public async Task UpdateAnnouncementAsync(Guid id, UpdateAnnouncementRequest request, ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length < 3 || request.Title.Length > 200)
            throw new ArgumentException("Nadpis musí mať 3-200 znakov");

        if (string.IsNullOrWhiteSpace(request.Content) || request.Content.Length < 10)
            throw new ArgumentException("Obsah musí mať minimálne 10 znakov");

        if (request.TargetTeamId.HasValue && !await _context.Teams.AnyAsync(t => t.Id == request.TargetTeamId.Value))
            throw new ArgumentException("Neplatný tím");

        var userId = user.GetUserId();
        var announcement = await _context.Announcements.FindAsync(id)
            ?? throw new KeyNotFoundException();

        if (announcement.AuthorUserId != userId && !user.IsInRole("Admin"))
            throw new UnauthorizedAccessException();

        announcement.Title = request.Title;
        announcement.Content = request.Content;
        announcement.TargetTeamId = request.TargetTeamId;
        announcement.TargetAudience = (Data.Entities.TargetAudience)(int)request.TargetAudience;
        announcement.Priority = (Data.Entities.AnnouncementPriority)(int)request.Priority;
        announcement.IsPinned = request.IsPinned;
        announcement.ValidUntil = request.ValidUntil;
        announcement.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAnnouncementAsync(Guid id, ClaimsPrincipal user)
    {
        var userId = user.GetUserId();
        var announcement = await _context.Announcements
            .Include(a => a.Attachments)
            .FirstOrDefaultAsync(a => a.Id == id)
            ?? throw new KeyNotFoundException();

        if (announcement.AuthorUserId != userId && !user.IsInRole("Admin"))
            throw new UnauthorizedAccessException();

        _context.AnnouncementAttachments.RemoveRange(announcement.Attachments);
        _context.Announcements.Remove(announcement);
        await _context.SaveChangesAsync();
    }

    public async Task<int> GetUnreadCountAsync()
    {
        // Placeholder: returns count of announcements from the last 7 days as a proxy for "unread".
        // Real per-user read-tracking is not implemented yet, so this number is not accurate.
        return await _context.Announcements.CountAsync(a => a.PublishDate >= DateTime.UtcNow.AddDays(-7));
    }

    public async Task<AttachmentDto> UploadAttachmentAsync(Guid announcementId, IFormFile file)
    {
        var announcement = await _context.Announcements.FindAsync(announcementId)
            ?? throw new KeyNotFoundException();

        var allowedExtensions = new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".jpg", ".jpeg", ".png", ".gif" };
        var (isValid, errorMessage) = _fileService.ValidateFile(file, 10 * 1024 * 1024, allowedExtensions);
        if (!isValid)
            throw new ArgumentException(errorMessage);

        var relativePath = await _fileService.SaveFileAsync(file, "attachments");

        var attachment = new AnnouncementAttachment
        {
            Id = Guid.NewGuid(),
            AnnouncementId = announcementId,
            FileName = file.FileName,
            FilePath = relativePath,
            ContentType = file.ContentType,
            FileSize = file.Length,
            UploadedAt = DateTime.UtcNow
        };

        _context.AnnouncementAttachments.Add(attachment);
        await _context.SaveChangesAsync();

        return new AttachmentDto(attachment.Id, attachment.FileName, attachment.ContentType, attachment.FileSize);
    }

    public async Task<(string FullPath, string ContentType, string FileName)> GetAttachmentFileAsync(Guid announcementId, Guid attachmentId)
    {
        var attachment = await _context.AnnouncementAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.AnnouncementId == announcementId)
            ?? throw new KeyNotFoundException();

        return (attachment.FilePath, attachment.ContentType ?? "application/octet-stream", attachment.FileName);
    }

    public async Task DeleteAttachmentAsync(Guid announcementId, Guid attachmentId)
    {
        var attachment = await _context.AnnouncementAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.AnnouncementId == announcementId)
            ?? throw new KeyNotFoundException();

        _fileService.DeleteFile(attachment.FilePath);

        _context.AnnouncementAttachments.Remove(attachment);
        await _context.SaveChangesAsync();
    }

    private static AnnouncementDto MapToDto(Announcement a) => new(
        a.Id, a.Title, a.Content, a.TargetTeamId, a.TargetTeam?.Name,
        (TargetAudience)(int)a.TargetAudience,
        (AnnouncementPriority)(int)a.Priority,
        a.IsPinned, a.ValidUntil, a.AuthorUserId,
        $"{a.AuthorUser.FirstName} {a.AuthorUser.LastName}",
        a.PublishDate, a.UpdatedAt, 0, false,
        a.Attachments.Select(att => new AttachmentDto(att.Id, att.FileName, att.ContentType, att.FileSize)).ToList()
    );
}
