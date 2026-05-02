using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZISK.Services;
using ZISK.Shared.DTOs.Stats;

namespace ZISK.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class StatsController : ControllerBase
{
    private readonly IStatsService _statsService;

    public StatsController(IStatsService statsService)
    {
        _statsService = statsService;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardStatsDto>> GetDashboardStats()
    {
        var result = await _statsService.GetDashboardStatsAsync();
        return Ok(result);
    }

    [HttpGet("teams")]
    public async Task<ActionResult<List<TeamStatsDto>>> GetTeamStats()
    {
        var result = await _statsService.GetTeamStatsAsync();
        return Ok(result);
    }

    [HttpGet("attendance")]
    public async Task<ActionResult<AttendanceStatsDto>> GetAttendanceStats([FromQuery] int days = 30)
    {
        var result = await _statsService.GetAttendanceStatsAsync(days);
        return Ok(result);
    }

    [HttpGet("training-types")]
    public async Task<ActionResult<List<TrainingTypeStatDto>>> GetTrainingTypeStats([FromQuery] int days = 30)
    {
        var result = await _statsService.GetTrainingTypeStatsAsync(days);
        return Ok(result);
    }

    [HttpGet("attendance-trend")]
    public async Task<ActionResult<List<AttendanceTrendPointDto>>> GetAttendanceTrend([FromQuery] int days = 30)
    {
        var result = await _statsService.GetAttendanceTrendAsync(days);
        return Ok(result);
    }
}
