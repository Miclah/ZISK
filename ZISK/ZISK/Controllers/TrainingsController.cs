using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Shared.DTOs.Trainings;
using ZISK.Shared.Enums;
using TrainingType = ZISK.Shared.Enums.TrainingType;
using AttendanceStatus = ZISK.Shared.Enums.AttendanceStatus;

namespace ZISK.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TrainingsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public TrainingsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<TrainingEventDto>>> GetTrainings(
        [FromQuery] Guid? teamId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var query = _context.TrainingEvents
            .Include(t => t.Team)
            .AsNoTracking();

        if (teamId.HasValue)
            query = query.Where(t => t.TeamId == teamId.Value);

        if (from.HasValue)
            query = query.Where(t => t.StartTime >= from.Value);

        if (to.HasValue)
            query = query.Where(t => t.StartTime <= to.Value);

        var trainings = await query
            .OrderByDescending(t => t.StartTime)
            .Select(t => new TrainingEventDto(
                t.Id,
                t.TeamId,
                t.Team.Name,
                t.Title,
                t.StartTime,
                t.EndTime,
                t.Location,
                (TrainingType)(int)t.Type,
                t.CoachNote,
                t.IsLocked
            ))
            .ToListAsync();

        return Ok(trainings);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TrainingEventDetailDto>> GetTraining(Guid id)
    {
        var training = await _context.TrainingEvents
            .Include(t => t.Team)
            .Include(t => t.AttendanceRecords)
                .ThenInclude(ar => ar.Child)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);

        if (training == null)
            return NotFound();

        
        var teamMembers = await _context.ChildProfiles
            .Where(c => c.TeamId == training.TeamId && c.IsActive)
            .ToListAsync();

        var excuses = await _context.AbsenceRequests
            .Where(ar => ar.TrainingEventId == id && ar.Status == AbsenceRequestStatus.Approved)
            .ToListAsync();

        var attendance = new List<TrainingAttendanceDto>();

        foreach (var member in teamMembers)
        {
            var record = training.AttendanceRecords.FirstOrDefault(ar => ar.ChildId == member.Id);
            var excuse = excuses.FirstOrDefault(e => e.ChildId == member.Id);

            attendance.Add(new TrainingAttendanceDto(
                member.Id,
                $"{member.FirstName} {member.LastName}",
                record != null ? (AttendanceStatus)(int)record.Status : AttendanceStatus.Absent,
                record?.Note,
                record?.CoachComment,
                excuse != null,
                excuse?.Reason
            ));
        }

        return Ok(new TrainingEventDetailDto(
            training.Id,
            training.TeamId,
            training.Team.Name,
            training.Title,
            training.StartTime,
            training.EndTime,
            training.Location,
            (TrainingType)(int)training.Type,
            training.CoachNote,
            training.IsLocked,
            training.CreatedAt,
            attendance.OrderBy(a => a.ChildName).ToList()
        ));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<ActionResult<TrainingEventDto>> CreateTraining([FromBody] CreateTrainingEventRequest request)
    {
        var team = await _context.Teams.FindAsync(request.TeamId);
        if (team == null)
            return BadRequest("Tím neexistuje");

        var training = new TrainingEvent
        {
            Id = Guid.NewGuid(),
            TeamId = request.TeamId,
            Title = request.Title,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Location = request.Location,
            Type = (Data.Entities.TrainingType)(int)request.Type,
            CoachNote = request.CoachNote,
            CreatedAt = DateTime.UtcNow
        };

        _context.TrainingEvents.Add(training);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetTraining), new { id = training.Id }, new TrainingEventDto(
            training.Id,
            training.TeamId,
            team.Name,
            training.Title,
            training.StartTime,
            training.EndTime,
            training.Location,
            (TrainingType)(int)training.Type,
            training.CoachNote,
            training.IsLocked
        ));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<IActionResult> UpdateTraining(Guid id, [FromBody] UpdateTrainingEventRequest request)
    {
        var training = await _context.TrainingEvents.FindAsync(id);
        if (training == null)
            return NotFound();

        training.Title = request.Title;
        training.StartTime = request.StartTime;
        training.EndTime = request.EndTime;
        training.Location = request.Location;
        training.Type = (Data.Entities.TrainingType)(int)request.Type;
        training.CoachNote = request.CoachNote;
        training.IsLocked = request.IsLocked;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPut("{id:guid}/lock")]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<IActionResult> LockTraining(Guid id)
    {
        var training = await _context.TrainingEvents.FindAsync(id);
        if (training == null)
            return NotFound();

        training.IsLocked = true;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPut("{id:guid}/unlock")]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<IActionResult> UnlockTraining(Guid id)
    {
        var training = await _context.TrainingEvents.FindAsync(id);
        if (training == null)
            return NotFound();

        training.IsLocked = false;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteTraining(Guid id)
    {
        var training = await _context.TrainingEvents.FindAsync(id);
        if (training == null)
            return NotFound();

        _context.TrainingEvents.Remove(training);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
