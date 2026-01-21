using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Shared.DTOs.Attendance;
using AttendanceStatus = ZISK.Shared.Enums.AttendanceStatus;

namespace ZISK.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AttendanceController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AttendanceController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("training/{trainingEventId:guid}")]
    public async Task<ActionResult<List<AttendanceRecordDto>>> GetTrainingAttendance(Guid trainingEventId)
    {
        var training = await _context.TrainingEvents
            .Include(t => t.Team)
            .FirstOrDefaultAsync(t => t.Id == trainingEventId);

        if (training == null)
            return NotFound("Tréning neexistuje");

        var attendance = await _context.AttendanceRecords
            .Include(ar => ar.Child)
            .Where(ar => ar.TrainingEventId == trainingEventId)
            .OrderBy(ar => ar.Child.LastName)
            .Select(ar => new AttendanceRecordDto(
                ar.Id,
                ar.TrainingEventId,
                training.Title,
                training.StartTime,
                ar.ChildId,
                $"{ar.Child.FirstName} {ar.Child.LastName}",
                (AttendanceStatus)(int)ar.Status,
                ar.Note,
                ar.CoachComment,
                ar.RecordedAt
            ))
            .ToListAsync();

        return Ok(attendance);
    }

    [HttpGet("my")]
    public async Task<ActionResult<List<UserAttendanceDto>>> GetMyAttendance(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var childIds = await _context.ParentChildren
            .Where(pc => pc.ParentId == userId)
            .Select(pc => pc.ChildId)
            .ToListAsync();

        if (!childIds.Any())
            return Ok(new List<UserAttendanceDto>());

        var query = _context.AttendanceRecords
            .Include(ar => ar.TrainingEvent)
            .Where(ar => childIds.Contains(ar.ChildId));

        if (from.HasValue)
            query = query.Where(ar => ar.TrainingEvent.StartTime >= from.Value);
        
        if (to.HasValue)
            query = query.Where(ar => ar.TrainingEvent.StartTime <= to.Value);

        var attendance = await query
            .OrderByDescending(ar => ar.TrainingEvent.StartTime)
            .Select(ar => new UserAttendanceDto(
                ar.Id,
                ar.TrainingEventId,
                ar.TrainingEvent.Title,
                ar.TrainingEvent.StartTime,
                (AttendanceStatus)(int)ar.Status,
                ar.Note,
                ar.CoachComment
            ))
            .ToListAsync();

        return Ok(attendance);
    }

    [HttpGet("stats/{childId:guid}")]
    public async Task<ActionResult<AttendanceStatsDto>> GetMemberStats(
        Guid childId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var query = _context.AttendanceRecords
            .Include(ar => ar.TrainingEvent)
            .Where(ar => ar.ChildId == childId);

        if (from.HasValue)
            query = query.Where(ar => ar.TrainingEvent.StartTime >= from.Value);
        
        if (to.HasValue)
            query = query.Where(ar => ar.TrainingEvent.StartTime <= to.Value);

        var records = await query.ToListAsync();

        // Pomoc s AI
        var stats = new AttendanceStatsDto(
            records.Count(r => r.Status == Data.Entities.AttendanceStatus.Present),
            records.Count(r => r.Status == Data.Entities.AttendanceStatus.Absent),
            records.Count(r => r.Status == Data.Entities.AttendanceStatus.Excused),
            records.Count(r => r.Status == Data.Entities.AttendanceStatus.Late),
            records.Count,
            records.Count > 0 
                ? Math.Round((double)records.Count(r => r.Status == Data.Entities.AttendanceStatus.Present || r.Status == Data.Entities.AttendanceStatus.Late) / records.Count * 100, 1)
                : 0
        );

        return Ok(stats);
    }

    [HttpGet("stats/team/{teamId:guid}")]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<ActionResult<List<MemberAttendanceStatsDto>>> GetTeamStats(
        Guid teamId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var members = await _context.ChildProfiles
            .Where(c => c.TeamId == teamId && c.IsActive)
            .ToListAsync();

        var result = new List<MemberAttendanceStatsDto>();

        foreach (var member in members)
        {
            var query = _context.AttendanceRecords
                .Include(ar => ar.TrainingEvent)
                .Where(ar => ar.ChildId == member.Id);

            if (from.HasValue)
                query = query.Where(ar => ar.TrainingEvent.StartTime >= from.Value);
            
            if (to.HasValue)
                query = query.Where(ar => ar.TrainingEvent.StartTime <= to.Value);

            var records = await query.ToListAsync();

            var present = records.Count(r => r.Status == Data.Entities.AttendanceStatus.Present);
            var absent = records.Count(r => r.Status == Data.Entities.AttendanceStatus.Absent);
            var excused = records.Count(r => r.Status == Data.Entities.AttendanceStatus.Excused);
            var late = records.Count(r => r.Status == Data.Entities.AttendanceStatus.Late);
            var total = records.Count;

            result.Add(new MemberAttendanceStatsDto(
                member.Id,
                $"{member.FirstName} {member.LastName}",
                present,
                absent,
                excused,
                late,
                total > 0 ? Math.Round((double)(present + late) / total * 100, 1) : 0
            ));
        }

        return Ok(result.OrderByDescending(r => r.AttendancePercentage).ToList());
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<ActionResult<AttendanceRecordDto>> MarkAttendance([FromBody] MarkAttendanceRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var training = await _context.TrainingEvents.FindAsync(request.TrainingEventId);
        if (training == null)
            return BadRequest("Tréning neexistuje");

        if (training.IsLocked)
            return BadRequest("Dochádzka pre tento tréning je uzamknutá");

        var child = await _context.ChildProfiles.FindAsync(request.ChildId);
        if (child == null)
            return BadRequest("Člen neexistuje");

        var existingRecord = await _context.AttendanceRecords
            .FirstOrDefaultAsync(ar => ar.TrainingEventId == request.TrainingEventId && ar.ChildId == request.ChildId);

        if (existingRecord != null)
        {

            existingRecord.Status = (Data.Entities.AttendanceStatus)(int)request.Status;
            existingRecord.Note = request.Note;
            existingRecord.CoachComment = request.CoachComment;
            existingRecord.MarkedByUserId = userId;
            existingRecord.RecordedAt = DateTime.UtcNow;
        }
        else
        {

            existingRecord = new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                TrainingEventId = request.TrainingEventId,
                ChildId = request.ChildId,
                Status = (Data.Entities.AttendanceStatus)(int)request.Status,
                Note = request.Note,
                CoachComment = request.CoachComment,
                MarkedByUserId = userId,
                RecordedAt = DateTime.UtcNow
            };
            _context.AttendanceRecords.Add(existingRecord);
        }

        await _context.SaveChangesAsync();

        return Ok(new AttendanceRecordDto(
            existingRecord.Id,
            existingRecord.TrainingEventId,
            training.Title,
            training.StartTime,
            existingRecord.ChildId,
            $"{child.FirstName} {child.LastName}",
            (AttendanceStatus)(int)existingRecord.Status,
            existingRecord.Note,
            existingRecord.CoachComment,
            existingRecord.RecordedAt
        ));
    }

    [HttpPost("bulk")]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<IActionResult> BulkMarkAttendance([FromBody] BulkMarkAttendanceRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var training = await _context.TrainingEvents.FindAsync(request.TrainingEventId);
        if (training == null)
            return BadRequest("Tréning neexistuje");

        if (training.IsLocked)
            return BadRequest("Dochádzka pre tento tréning je uzamknutá");

        foreach (var entry in request.Entries)
        {
            var existingRecord = await _context.AttendanceRecords
                .FirstOrDefaultAsync(ar => ar.TrainingEventId == request.TrainingEventId && ar.ChildId == entry.ChildId);

            if (existingRecord != null)
            {
                existingRecord.Status = (Data.Entities.AttendanceStatus)(int)entry.Status;
                existingRecord.Note = entry.Note;
                existingRecord.MarkedByUserId = userId;
                existingRecord.RecordedAt = DateTime.UtcNow;
            }
            else
            {
                var newRecord = new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    TrainingEventId = request.TrainingEventId,
                    ChildId = entry.ChildId,
                    Status = (Data.Entities.AttendanceStatus)(int)entry.Status,
                    Note = entry.Note,
                    MarkedByUserId = userId,
                    RecordedAt = DateTime.UtcNow
                };
                _context.AttendanceRecords.Add(newRecord);
            }
        }

        await _context.SaveChangesAsync();

        return Ok();
    }
}
