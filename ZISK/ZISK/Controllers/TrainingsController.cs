using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZISK.Services;
using ZISK.Shared.DTOs.Trainings;

namespace ZISK.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TrainingsController : ControllerBase
{
    private readonly ITrainingService _trainingService;

    public TrainingsController(ITrainingService trainingService)
    {
        _trainingService = trainingService;
    }

    [HttpGet]
    public async Task<ActionResult<List<TrainingEventDto>>> GetTrainings(
        [FromQuery] Guid? teamId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var result = await _trainingService.GetTrainingsAsync(teamId, from, to, User);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TrainingEventDetailDto>> GetTraining(Guid id)
    {
        try
        {
            var result = await _trainingService.GetTrainingAsync(id, User);
            return Ok(result);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<ActionResult<TrainingEventDto>> CreateTraining([FromBody] CreateTrainingEventRequest request)
    {
        try
        {
            var result = await _trainingService.CreateTrainingAsync(request, User);
            return CreatedAtAction(nameof(GetTraining), new { id = result.Id }, result);
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<IActionResult> UpdateTraining(Guid id, [FromBody] UpdateTrainingEventRequest request)
    {
        try
        {
            await _trainingService.UpdateTrainingAsync(id, request, User);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<IActionResult> CancelTraining(Guid id, [FromBody] CancelTrainingRequest request)
    {
        try
        {
            await _trainingService.CancelTrainingAsync(id, request, User);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPut("{id:guid}/lock")]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<IActionResult> LockTraining(Guid id)
    {
        try
        {
            await _trainingService.LockTrainingAsync(id, User);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPut("{id:guid}/unlock")]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<IActionResult> UnlockTraining(Guid id)
    {
        try
        {
            await _trainingService.UnlockTrainingAsync(id, User);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteTraining(Guid id)
    {
        try
        {
            await _trainingService.DeleteTrainingAsync(id, User);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
