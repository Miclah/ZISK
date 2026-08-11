using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZISK.Services;
using ZISK.Shared.DTOs.Attendance;

namespace ZISK.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AttendanceController : ControllerBase
{
    private readonly IAttendanceService _attendanceService;

    public AttendanceController(IAttendanceService attendanceService)
    {
        _attendanceService = attendanceService;
    }

    [HttpGet("training/{trainingEventId:guid}")]
    public async Task<ActionResult<List<AttendanceRecordDto>>> GetTrainingAttendance(Guid trainingEventId)
    {
        try
        {
            var result = await _attendanceService.GetTrainingAttendanceAsync(trainingEventId, User);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("my")]
    public async Task<ActionResult<List<UserAttendanceDto>>> GetMyAttendance(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        try
        {
            var result = await _attendanceService.GetMyAttendanceAsync(User, from, to);
            return Ok(result);
        }
        catch (UnauthorizedAccessException) { return Unauthorized(); }
    }

    [HttpGet("stats/{childId}")]
    public async Task<ActionResult<AttendanceStatsDto>> GetMemberStats(
        string childId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        try
        {
            var result = await _attendanceService.GetMemberStatsAsync(childId, from, to, User);
            return Ok(result);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("stats/team/{teamId:guid}")]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<ActionResult<List<MemberAttendanceStatsDto>>> GetTeamStats(
        Guid teamId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        try
        {
            var result = await _attendanceService.GetTeamStatsAsync(teamId, from, to, User);
            return Ok(result);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<ActionResult<AttendanceRecordDto>> MarkAttendance([FromBody] MarkAttendanceRequest request)
    {
        try
        {
            var result = await _attendanceService.MarkAttendanceAsync(request, User);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return BadRequest(ex.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("bulk")]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<IActionResult> BulkMarkAttendance([FromBody] BulkMarkAttendanceRequest request)
    {
        try
        {
            await _attendanceService.BulkMarkAttendanceAsync(request, User);
            return Ok();
        }
        catch (KeyNotFoundException ex) { return BadRequest(ex.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }
}
