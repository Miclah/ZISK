using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ZISK.Data;
using ZISK.Data.Entities;

namespace ZISK.Services.Demo;

public class DemoSessionService : IDemoSessionService
{
    private static readonly Dictionary<string, string> RoleToTemplateUserName = new()
    {
        ["Admin"] = "admin@zisk.sk",
        ["Coach"] = "trener@zisk.sk",
        ["Parent"] = "rodic@zisk.sk",
        ["Child"] = "dieta@zisk.sk",
    };

    private readonly ApplicationDbContext _context;
    private readonly IFileService _fileService;
    private readonly DemoOptions _options;
    private readonly ILogger<DemoSessionService> _logger;

    public DemoSessionService(
        ApplicationDbContext context,
        IFileService fileService,
        Microsoft.Extensions.Options.IOptions<DemoOptions> options,
        ILogger<DemoSessionService> logger)
    {
        _context = context;
        _fileService = fileService;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>The username/email prefix stamped on every user cloned into this session.</summary>
    public static string Prefix(Guid sessionId) => sessionId.ToString("N")[..8];

    public async Task<Guid> EnsureSessionAsync(Guid? cookieSessionId, string? createdFromRole = null)
    {
        if (cookieSessionId.HasValue)
        {
            var exists = await _context.DemoSessions.AnyAsync(s => s.Id == cookieSessionId.Value);
            if (exists)
            {
                await TouchAsync(cookieSessionId.Value);
                return cookieSessionId.Value;
            }
        }

        return await CloneTemplateIntoNewSessionAsync(createdFromRole);
    }

    public async Task<ApplicationUser?> GetUserForRoleAsync(Guid sessionId, string role)
    {
        if (!RoleToTemplateUserName.TryGetValue(role, out var templateUserName))
            return null;

        var newUserName = $"{Prefix(sessionId)}.{templateUserName}";
        return await _context.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.DemoSessionId == sessionId && u.UserName == newUserName);
    }

    public async Task TouchAsync(Guid sessionId)
    {
        var session = await _context.DemoSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session == null)
            return;

        session.LastSeenAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task<Guid> ResetSessionAsync(Guid sessionId)
    {
        var createdFromRole = await _context.DemoSessions
            .Where(s => s.Id == sessionId)
            .Select(s => s.CreatedFromRole)
            .FirstOrDefaultAsync();

        await DeleteSessionDataAsync(sessionId, alsoDeleteSessionRow: true);
        return await CloneTemplateIntoNewSessionAsync(createdFromRole);
    }

    public async Task DeleteSessionDataAsync(Guid sessionId, bool alsoDeleteSessionRow)
    {
        // Loaded via RemoveRange + a single SaveChangesAsync rather than ExecuteDeleteAsync:
        // the EF Core InMemory provider (used by this project's whole test suite) doesn't
        // support ExecuteDelete/ExecuteUpdate at all, and at demo-session scale (a few hundred
        // rows) the extra round trip to load rows first is not a real cost. SaveChanges also
        // works out a correct FK-safe delete order from the model on its own.
        var attachments = await _context.AnnouncementAttachments.IgnoreQueryFilters()
            .Where(a => a.DemoSessionId == sessionId).ToListAsync();
        var documents = await _context.Documents.IgnoreQueryFilters()
            .Where(d => d.DemoSessionId == sessionId).ToListAsync();

        // Clean up physical files before the rows that reference them go. Best-effort: a
        // failed delete here must not block the DB cleanup, it just leaves an orphaned file
        // (same trade-off FileService.DeleteFile already makes elsewhere in the app).
        foreach (var path in attachments.Select(a => a.FilePath).Concat(documents.Select(d => d.FilePath)).Distinct())
        {
            try { _fileService.DeleteFile(path); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete demo session file {Path}", path); }
        }

        var attendance = await _context.AttendanceRecords.IgnoreQueryFilters().Where(x => x.DemoSessionId == sessionId).ToListAsync();
        var absences = await _context.AbsenceRequests.IgnoreQueryFilters().Where(x => x.DemoSessionId == sessionId).ToListAsync();
        var announcements = await _context.Announcements.IgnoreQueryFilters().Where(x => x.DemoSessionId == sessionId).ToListAsync();
        var invitations = await _context.ParentInvitations.IgnoreQueryFilters().Where(x => x.DemoSessionId == sessionId).ToListAsync();
        var trainings = await _context.TrainingEvents.IgnoreQueryFilters().Where(x => x.DemoSessionId == sessionId).ToListAsync();
        var series = await _context.TrainingSeries.IgnoreQueryFilters().Where(x => x.DemoSessionId == sessionId).ToListAsync();
        var members = await _context.TeamMembers.IgnoreQueryFilters().Where(x => x.DemoSessionId == sessionId).ToListAsync();
        var coachTeams = await _context.CoachTeams.IgnoreQueryFilters().Where(x => x.DemoSessionId == sessionId).ToListAsync();
        var parentLinks = await _context.ParentChildren.IgnoreQueryFilters().Where(x => x.DemoSessionId == sessionId).ToListAsync();
        var users = await _context.Users.IgnoreQueryFilters().Where(u => u.DemoSessionId == sessionId).ToListAsync();
        var teams = await _context.Teams.IgnoreQueryFilters().Where(x => x.DemoSessionId == sessionId).ToListAsync();
        var seasons = await _context.Seasons.IgnoreQueryFilters().Where(x => x.DemoSessionId == sessionId).ToListAsync();

        var userIds = users.Select(u => u.Id).ToHashSet();
        var roles = userIds.Count > 0 ? await _context.UserRoles.Where(ur => userIds.Contains(ur.UserId)).ToListAsync() : [];
        var claims = userIds.Count > 0 ? await _context.UserClaims.Where(uc => userIds.Contains(uc.UserId)).ToListAsync() : [];
        var logins = userIds.Count > 0 ? await _context.UserLogins.Where(ul => userIds.Contains(ul.UserId)).ToListAsync() : [];
        var tokens = userIds.Count > 0 ? await _context.UserTokens.Where(ut => userIds.Contains(ut.UserId)).ToListAsync() : [];

        _context.AttendanceRecords.RemoveRange(attendance);
        _context.AbsenceRequests.RemoveRange(absences);
        _context.AnnouncementAttachments.RemoveRange(attachments);
        _context.Announcements.RemoveRange(announcements);
        _context.Documents.RemoveRange(documents);
        _context.ParentInvitations.RemoveRange(invitations);
        _context.TrainingEvents.RemoveRange(trainings);
        _context.TrainingSeries.RemoveRange(series);
        _context.TeamMembers.RemoveRange(members);
        _context.CoachTeams.RemoveRange(coachTeams);
        _context.ParentChildren.RemoveRange(parentLinks);
        _context.UserRoles.RemoveRange(roles);
        _context.UserClaims.RemoveRange(claims);
        _context.UserLogins.RemoveRange(logins);
        _context.UserTokens.RemoveRange(tokens);
        _context.Users.RemoveRange(users);
        _context.Teams.RemoveRange(teams);
        _context.Seasons.RemoveRange(seasons);

        if (alsoDeleteSessionRow)
        {
            var session = await _context.DemoSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
            if (session != null)
                _context.DemoSessions.Remove(session);
        }

        await _context.SaveChangesAsync();
    }

    private async Task EnforceSessionCapAsync()
    {
        if (_options.MaxActiveSessions <= 0)
            return;

        var count = await _context.DemoSessions.CountAsync();
        if (count < _options.MaxActiveSessions)
            return;

        var oldest = await _context.DemoSessions.OrderBy(s => s.LastSeenAt).FirstOrDefaultAsync();
        if (oldest != null)
        {
            _logger.LogInformation("Demo session cap ({Max}) reached; evicting oldest session {SessionId}",
                _options.MaxActiveSessions, oldest.Id);
            await DeleteSessionDataAsync(oldest.Id, alsoDeleteSessionRow: true);
        }
    }

    private async Task<Guid> CloneTemplateIntoNewSessionAsync(string? createdFromRole)
    {
        await EnforceSessionCapAsync();

        var sessionId = Guid.NewGuid();
        var prefix = Prefix(sessionId);
        var now = DateTime.UtcNow;

        _context.DemoSessions.Add(new DemoSession
        {
            Id = sessionId,
            CreatedAt = now,
            LastSeenAt = now,
            CreatedFromRole = createdFromRole
        });

        // ---- Season (at most one active template season) ----
        var templateSeason = await _context.Seasons.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.DemoSessionId == null && s.IsActive);
        Guid? newSeasonId = null;
        if (templateSeason != null)
        {
            newSeasonId = Guid.NewGuid();
            _context.Seasons.Add(new Season
            {
                Id = newSeasonId.Value,
                DemoSessionId = sessionId,
                Name = templateSeason.Name,
                StartDate = templateSeason.StartDate,
                EndDate = templateSeason.EndDate,
                IsActive = templateSeason.IsActive,
                CreatedAt = templateSeason.CreatedAt
            });
        }

        // ---- Teams ----
        var templateTeams = await _context.Teams.IgnoreQueryFilters()
            .Where(t => t.DemoSessionId == null).ToListAsync();
        var teamIdMap = new Dictionary<Guid, Guid>();
        foreach (var t in templateTeams)
        {
            var newId = Guid.NewGuid();
            teamIdMap[t.Id] = newId;
            _context.Teams.Add(new Team
            {
                Id = newId,
                DemoSessionId = sessionId,
                Name = t.Name,
                ShortName = t.ShortName,
                Description = t.Description,
                IsActive = t.IsActive,
                CreatedAt = t.CreatedAt
            });
        }

        // ---- Users + roles. UserName/Email are prefixed because AspNetUsers.NormalizedUserName
        // carries Identity's own (non demo-scoped) unique index - two sessions cloning the same
        // "admin@zisk.sk" verbatim would collide at the DB level. ----
        var templateUsers = await _context.Users.IgnoreQueryFilters()
            .Where(u => u.DemoSessionId == null).ToListAsync();
        var userIdMap = new Dictionary<string, string>();
        foreach (var u in templateUsers)
        {
            var newId = Guid.NewGuid().ToString();
            userIdMap[u.Id] = newId;
            var newUserName = $"{prefix}.{u.UserName}";
            var newEmail = $"{prefix}.{u.Email}";

            _context.Users.Add(new ApplicationUser
            {
                Id = newId,
                DemoSessionId = sessionId,
                UserName = newUserName,
                NormalizedUserName = newUserName.ToUpperInvariant(),
                Email = newEmail,
                NormalizedEmail = newEmail.ToUpperInvariant(),
                EmailConfirmed = u.EmailConfirmed,
                PasswordHash = u.PasswordHash,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString(),
                PhoneNumber = u.PhoneNumber,
                PhoneNumberConfirmed = u.PhoneNumberConfirmed,
                TwoFactorEnabled = false,
                LockoutEnabled = u.LockoutEnabled,
                LockoutEnd = null,
                AccessFailedCount = 0,
                FirstName = u.FirstName,
                LastName = u.LastName,
                RodneCislo = u.RodneCislo,
                Bydlisko = u.Bydlisko,
                DateOfBirth = u.DateOfBirth,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt
            });
        }

        var templateUserIds = templateUsers.Select(u => u.Id).ToList();
        var templateRoles = await _context.UserRoles
            .Where(ur => templateUserIds.Contains(ur.UserId)).ToListAsync();
        foreach (var ur in templateRoles)
        {
            if (userIdMap.TryGetValue(ur.UserId, out var newUserId))
                _context.UserRoles.Add(new IdentityUserRole<string> { UserId = newUserId, RoleId = ur.RoleId });
        }

        // ---- TeamMembers ----
        var templateMembers = await _context.TeamMembers.IgnoreQueryFilters()
            .Where(tm => tm.DemoSessionId == null).ToListAsync();
        foreach (var tm in templateMembers)
        {
            if (!teamIdMap.TryGetValue(tm.TeamId, out var newTeamId)) continue;
            if (!userIdMap.TryGetValue(tm.UserId, out var newUserId)) continue;

            _context.TeamMembers.Add(new TeamMember
            {
                DemoSessionId = sessionId,
                TeamId = newTeamId,
                UserId = newUserId,
                JoinedAt = tm.JoinedAt
            });
        }

        // ---- CoachTeams ----
        var templateCoachTeams = await _context.CoachTeams.IgnoreQueryFilters()
            .Where(ct => ct.DemoSessionId == null).ToListAsync();
        foreach (var ct in templateCoachTeams)
        {
            if (!teamIdMap.TryGetValue(ct.TeamId, out var newTeamId)) continue;
            if (!userIdMap.TryGetValue(ct.CoachId, out var newCoachId)) continue;

            _context.CoachTeams.Add(new CoachTeam
            {
                Id = Guid.NewGuid(),
                DemoSessionId = sessionId,
                CoachId = newCoachId,
                TeamId = newTeamId,
                IsPrimary = ct.IsPrimary,
                AssignedAt = ct.AssignedAt
            });
        }

        // ---- ParentChildren ----
        var templateLinks = await _context.ParentChildren.IgnoreQueryFilters()
            .Where(pc => pc.DemoSessionId == null).ToListAsync();
        foreach (var pc in templateLinks)
        {
            if (!userIdMap.TryGetValue(pc.ParentId, out var newParentId)) continue;
            if (!userIdMap.TryGetValue(pc.ChildId, out var newChildId)) continue;

            _context.ParentChildren.Add(new ParentChild
            {
                DemoSessionId = sessionId,
                ParentId = newParentId,
                ChildId = newChildId,
                IsPrimary = pc.IsPrimary
            });
        }

        // ---- TrainingSeries (none in the seeded template today, but handled for completeness -
        // an owner logged in via /login could create one against the template) ----
        var templateSeries = await _context.TrainingSeries.IgnoreQueryFilters()
            .Where(ts => ts.DemoSessionId == null).ToListAsync();
        var seriesIdMap = new Dictionary<Guid, Guid>();
        foreach (var ts in templateSeries)
        {
            if (newSeasonId == null) continue;
            if (!teamIdMap.TryGetValue(ts.TeamId, out var newTeamId)) continue;
            if (!userIdMap.TryGetValue(ts.CoachId, out var newCoachId)) continue;

            var newId = Guid.NewGuid();
            seriesIdMap[ts.Id] = newId;
            _context.TrainingSeries.Add(new TrainingSeries
            {
                Id = newId,
                DemoSessionId = sessionId,
                TeamId = newTeamId,
                CoachId = newCoachId,
                SeasonId = newSeasonId.Value,
                Title = ts.Title,
                DaysOfWeek = ts.DaysOfWeek,
                StartTime = ts.StartTime,
                EndTime = ts.EndTime,
                Location = ts.Location,
                Type = ts.Type,
                CoachNote = ts.CoachNote,
                IsActive = ts.IsActive,
                CreatedAt = ts.CreatedAt
            });
        }

        // ---- TrainingEvents ----
        var templateTrainings = await _context.TrainingEvents.IgnoreQueryFilters()
            .Where(te => te.DemoSessionId == null).ToListAsync();
        var trainingIdMap = new Dictionary<Guid, Guid>();
        foreach (var te in templateTrainings)
        {
            if (newSeasonId == null) continue;
            if (!teamIdMap.TryGetValue(te.TeamId, out var newTeamId)) continue;

            var newId = Guid.NewGuid();
            trainingIdMap[te.Id] = newId;
            _context.TrainingEvents.Add(new TrainingEvent
            {
                Id = newId,
                DemoSessionId = sessionId,
                TeamId = newTeamId,
                SeriesId = te.SeriesId.HasValue && seriesIdMap.TryGetValue(te.SeriesId.Value, out var newSeriesId) ? newSeriesId : null,
                SeasonId = newSeasonId.Value,
                Title = te.Title,
                StartTime = te.StartTime,
                EndTime = te.EndTime,
                Location = te.Location,
                Type = te.Type,
                CoachNote = te.CoachNote,
                IsLocked = te.IsLocked,
                IsCancelled = te.IsCancelled,
                CancelledReason = te.CancelledReason,
                CreatedAt = te.CreatedAt
            });
        }

        // ---- AttendanceRecords ----
        var templateAttendance = await _context.AttendanceRecords.IgnoreQueryFilters()
            .Where(ar => ar.DemoSessionId == null).ToListAsync();
        foreach (var ar in templateAttendance)
        {
            if (!trainingIdMap.TryGetValue(ar.TrainingEventId, out var newTrainingId)) continue;
            if (!userIdMap.TryGetValue(ar.ChildId, out var newChildId)) continue;

            _context.AttendanceRecords.Add(new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                DemoSessionId = sessionId,
                TrainingEventId = newTrainingId,
                ChildId = newChildId,
                Status = ar.Status,
                Note = ar.Note,
                CoachComment = ar.CoachComment,
                MarkedByUserId = ar.MarkedByUserId != null && userIdMap.TryGetValue(ar.MarkedByUserId, out var newMarkedBy) ? newMarkedBy : null,
                RecordedAt = ar.RecordedAt
            });
        }

        // ---- AbsenceRequests ----
        var templateAbsences = await _context.AbsenceRequests.IgnoreQueryFilters()
            .Where(x => x.DemoSessionId == null).ToListAsync();
        foreach (var a in templateAbsences)
        {
            if (!userIdMap.TryGetValue(a.ChildId, out var newChildId)) continue;
            if (!userIdMap.TryGetValue(a.ParentId, out var newParentId)) continue;

            Guid? newTrainingEventId = a.TrainingEventId.HasValue
                && trainingIdMap.TryGetValue(a.TrainingEventId.Value, out var nt) ? nt : null;

            _context.AbsenceRequests.Add(new AbsenceRequest
            {
                Id = Guid.NewGuid(),
                DemoSessionId = sessionId,
                ChildId = newChildId,
                ParentId = newParentId,
                TrainingEventId = newTrainingEventId,
                DateFrom = a.DateFrom,
                DateTo = a.DateTo,
                Reason = a.Reason,
                Note = a.Note,
                Status = a.Status,
                ReviewedByUserId = a.ReviewedByUserId != null && userIdMap.TryGetValue(a.ReviewedByUserId, out var newRev) ? newRev : null,
                ReviewNote = a.ReviewNote,
                ProcessedAt = a.ProcessedAt,
                CreatedAt = a.CreatedAt
            });
        }

        // ---- Announcements + attachments ----
        var templateAnnouncements = await _context.Announcements.IgnoreQueryFilters()
            .Where(x => x.DemoSessionId == null).ToListAsync();
        var announcementIdMap = new Dictionary<Guid, Guid>();
        foreach (var an in templateAnnouncements)
        {
            if (!userIdMap.TryGetValue(an.AuthorUserId, out var newAuthorId)) continue;

            var newId = Guid.NewGuid();
            announcementIdMap[an.Id] = newId;
            _context.Announcements.Add(new Announcement
            {
                Id = newId,
                DemoSessionId = sessionId,
                Title = an.Title,
                Content = an.Content,
                TargetTeamId = an.TargetTeamId.HasValue && teamIdMap.TryGetValue(an.TargetTeamId.Value, out var newTargetTeam) ? newTargetTeam : null,
                TargetAudience = an.TargetAudience,
                Priority = an.Priority,
                IsPinned = an.IsPinned,
                ValidUntil = an.ValidUntil,
                AuthorUserId = newAuthorId,
                PublishDate = an.PublishDate,
                UpdatedAt = an.UpdatedAt
            });
        }

        // Not expected to have any rows today (the seeded template announcements carry no
        // attachments), handled anyway so an owner-added attachment clones correctly too. Note
        // the cloned row points at the SAME physical file as every other session's copy - if
        // one session's clone is later deleted, DeleteSessionDataAsync will delete that shared
        // file out from under the others. Accepted trade-off: demo mode blocks new uploads
        // (see AnnouncementsCreate.razor / DocumentsController), so this can only happen to
        // pre-existing template attachments, which is not a path the current template exercises.
        var templateAttachments = await _context.AnnouncementAttachments.IgnoreQueryFilters()
            .Where(x => x.DemoSessionId == null).ToListAsync();
        foreach (var att in templateAttachments)
        {
            if (!announcementIdMap.TryGetValue(att.AnnouncementId, out var newAnnouncementId)) continue;

            _context.AnnouncementAttachments.Add(new AnnouncementAttachment
            {
                Id = Guid.NewGuid(),
                DemoSessionId = sessionId,
                AnnouncementId = newAnnouncementId,
                FileName = att.FileName,
                FilePath = att.FilePath,
                ContentType = att.ContentType,
                FileSize = att.FileSize,
                UploadedAt = att.UploadedAt
            });
        }

        // ---- Documents (same shared-file caveat as attachments above) ----
        var templateDocuments = await _context.Documents.IgnoreQueryFilters()
            .Where(x => x.DemoSessionId == null).ToListAsync();
        foreach (var d in templateDocuments)
        {
            _context.Documents.Add(new Document
            {
                Id = Guid.NewGuid(),
                DemoSessionId = sessionId,
                Title = d.Title,
                FilePath = d.FilePath,
                Category = d.Category,
                TargetRoleId = d.TargetRoleId,
                UploadedByUserId = d.UploadedByUserId != null && userIdMap.TryGetValue(d.UploadedByUserId, out var newUploader) ? newUploader : null,
                UploadedAt = d.UploadedAt
            });
        }

        await _context.SaveChangesAsync();
        return sessionId;
    }
}
