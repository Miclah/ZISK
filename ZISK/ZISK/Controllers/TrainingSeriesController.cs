using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ZISK.Services;
using ZISK.Shared.DTOs.Trainings;

namespace ZISK.Controllers;

[ApiController]
[Route("api/training-series")]
[Authorize(Roles = "Admin,Coach")]
public class TrainingSeriesController : ControllerBase
{
    private readonly ITrainingSeriesService _seriesService;

    public TrainingSeriesController(ITrainingSeriesService seriesService)
    {
        _seriesService = seriesService;
    }

    [HttpGet]
    public async Task<ActionResult<List<TrainingSeriesDto>>> GetSeries(
        [FromQuery] Guid? teamId = null,
        [FromQuery] Guid? seasonId = null)
    {
        var result = await _seriesService.GetSeriesAsync(User, teamId, seasonId);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TrainingSeriesDto>> GetSeries(Guid id)
    {
        try { return Ok(await _seriesService.GetSeriesAsync(id, User)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost]
    public async Task<ActionResult<TrainingSeriesDto>> CreateSeries([FromBody] CreateTrainingSeriesRequest request)
    {
        var coachId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        try
        {
            var result = await _seriesService.CreateSeriesAsync(request, coachId, User);
            return CreatedAtAction(nameof(GetSeries), new { id = result.Id }, result);
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TrainingSeriesDto>> UpdateSeries(Guid id, [FromBody] UpdateTrainingSeriesRequest request)
    {
        try { return Ok(await _seriesService.UpdateSeriesAsync(id, request, User)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteSeries(Guid id)
    {
        try { await _seriesService.DeleteSeriesAsync(id, User); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost("{id:guid}/generate")]
    public async Task<ActionResult<int>> GenerateInstances(Guid id, [FromBody] GenerateInstancesRequest request)
    {
        try
        {
            var count = await _seriesService.GenerateInstancesAsync(id, request.From, request.To, User);
            return Ok(count);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
