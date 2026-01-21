using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ZISK.Data;
using ZISK.Data.Entities;
using ZISK.Shared.DTOs.Excuses;
using ExcuseStatus = ZISK.Shared.Enums.ExcuseStatus;

namespace ZISK.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExcusesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ExcusesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<ExcuseListDto>>> GetExcuses([FromQuery] ExcuseStatus? status = null)
    {
        var query = _context.AbsenceRequests
            .Include(ar => ar.Child)
                .ThenInclude(c => c.Team)
            .AsNoTracking();

        if (status.HasValue)
        {
            var dbStatus = (AbsenceRequestStatus)(int)status.Value;
            query = query.Where(ar => ar.Status == dbStatus);
        }

        var excuses = await query
            .OrderByDescending(ar => ar.CreatedAt)
            .Select(ar => new ExcuseListDto(
                ar.Id,
                $"{ar.Child.FirstName} {ar.Child.LastName}",
                ar.Child.Team != null ? ar.Child.Team.Name : "Bez tímu",
                ar.DateFrom,
                ar.DateTo,
                ar.Reason,
                (ExcuseStatus)(int)ar.Status,
                ar.CreatedAt
            ))
            .ToListAsync();

        return Ok(excuses);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ExcuseDto>> GetExcuse(Guid id)
    {
        var excuse = await _context.AbsenceRequests
            .Include(ar => ar.Child)
                .ThenInclude(c => c.Team)
            .Include(ar => ar.TrainingEvent)
            .Include(ar => ar.ReviewedByUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(ar => ar.Id == id);

        if (excuse == null)
            return NotFound();

        return Ok(new ExcuseDto(
            excuse.Id,
            excuse.ChildId,
            $"{excuse.Child.FirstName} {excuse.Child.LastName}",
            excuse.TrainingEventId,
            excuse.TrainingEvent?.Title,
            excuse.DateFrom,
            excuse.DateTo,
            excuse.Reason,
            excuse.Note,
            (ExcuseStatus)(int)excuse.Status,
            excuse.ReviewNote,
            excuse.ReviewedByUser != null ? $"{excuse.ReviewedByUser.FirstName} {excuse.ReviewedByUser.LastName}" : null,
            excuse.ProcessedAt,
            excuse.CreatedAt,
            excuse.Child.Team?.Name ?? "Bez tímu"
        ));
    }

    [HttpPost]
    public async Task<ActionResult<ExcuseDto>> CreateExcuse([FromBody] CreateExcuseRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length < 3 || request.Reason.Length > 200)
            return BadRequest("Dôvod musí mať 3-200 znakov");

        if (request.Note != null && request.Note.Length > 500)
            return BadRequest("Poznámka môže mať max 500 znakov");

        if (request.DateFrom.HasValue && request.DateFrom.Value < DateTime.UtcNow.AddDays(-30))
            return BadRequest("Dátum nemôže byť starší ako 30 dní");

        if (request.DateTo.HasValue && request.DateFrom.HasValue && request.DateTo.Value < request.DateFrom.Value)
            return BadRequest("Dátum 'Do' nemôže byť pred dátumom 'Od'");

        var child = await _context.ChildProfiles
            .Include(c => c.Team)
            .FirstOrDefaultAsync(c => c.Id == request.ChildId);

        if (child == null)
            return BadRequest("Dieťa neexistuje");

       
        if (request.TrainingEventId == null && request.DateFrom == null)
            return BadRequest("Musíte zadať buď konkrétny tréning alebo dátum absencie");

        var absence = new AbsenceRequest
        {
            Id = Guid.NewGuid(),
            ChildId = request.ChildId,
            ParentId = userId,
            TrainingEventId = request.TrainingEventId,
            DateFrom = request.DateFrom,
            DateTo = request.DateTo,
            Reason = request.Reason,
            Note = request.Note,
            Status = AbsenceRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.AbsenceRequests.Add(absence);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetExcuse), new { id = absence.Id }, new ExcuseDto(
            absence.Id,
            absence.ChildId,
            $"{child.FirstName} {child.LastName}",
            absence.TrainingEventId,
            null,
            absence.DateFrom,
            absence.DateTo,
            absence.Reason,
            absence.Note,
            ExcuseStatus.Pending,
            null,
            null,
            null,
            absence.CreatedAt,
            child.Team?.Name ?? "Bez tímu"
        ));
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateExcuseStatusRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var excuse = await _context.AbsenceRequests.FindAsync(id);
        
        if (excuse == null)
            return NotFound();

        excuse.Status = (AbsenceRequestStatus)(int)request.Status;
        excuse.ReviewNote = request.ReviewNote;
        excuse.ReviewedByUserId = userId;
        excuse.ProcessedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateExcuse(Guid id, [FromBody] UpdateExcuseRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length < 3 || request.Reason.Length > 200)
                return BadRequest("Dôvod musí mať 3-200 znakov");

            if (request.Note != null && request.Note.Length > 500)
                return BadRequest("Poznámka môže mať max 500 znakov");

            if (request.DateTo.HasValue && request.DateFrom.HasValue && request.DateTo.Value < request.DateFrom.Value)
                return BadRequest("Dátum 'Do' nemôže byť pred dátumom 'Od'");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var excuse = await _context.AbsenceRequests.FindAsync(id);
        
            if (excuse == null)
                return NotFound();

            if (excuse.ParentId != userId && !User.IsInRole("Admin"))
                return Forbid();

            if (excuse.Status != AbsenceRequestStatus.Pending)
                return BadRequest("Len čakajúce ospravedlnenky môžu byť upravené");

            excuse.DateFrom = request.DateFrom;
            excuse.DateTo = request.DateTo;
            excuse.Reason = request.Reason;
            excuse.Note = request.Note;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteExcuse(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var excuse = await _context.AbsenceRequests.FindAsync(id);
        
        if (excuse == null)
            return NotFound();

        if (excuse.ParentId != userId && !User.IsInRole("Admin"))
            return Forbid();

        if (excuse.Status != AbsenceRequestStatus.Pending)
            return BadRequest("Len čakajúce ospravedlnenky môžu byť vymazané");

        _context.AbsenceRequests.Remove(excuse);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("my")]
    public async Task<ActionResult<List<ExcuseListDto>>> GetMyExcuses()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var excuses = await _context.AbsenceRequests
            .Include(ar => ar.Child)
                .ThenInclude(c => c.Team)
            .Where(ar => ar.ParentId == userId)
            .OrderByDescending(ar => ar.CreatedAt)
            .Select(ar => new ExcuseListDto(
                ar.Id,
                $"{ar.Child.FirstName} {ar.Child.LastName}",
                ar.Child.Team != null ? ar.Child.Team.Name : "Bez tímu",
                ar.DateFrom,
                ar.DateTo,
                ar.Reason,
                (ExcuseStatus)(int)ar.Status,
                ar.CreatedAt
            ))
            .ToListAsync();

        return Ok(excuses);
    }

    [HttpGet("pending/count")]
    public async Task<ActionResult<int>> GetPendingCount()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        
        int count;
        if (User.IsInRole("Admin") || User.IsInRole("Coach"))
        {
            count = await _context.AbsenceRequests
                .CountAsync(ar => ar.Status == AbsenceRequestStatus.Pending);
        }
        else
        {
            count = await _context.AbsenceRequests
                .CountAsync(ar => ar.ParentId == userId && ar.Status == AbsenceRequestStatus.Pending);
        }

        return Ok(count);
    }
}