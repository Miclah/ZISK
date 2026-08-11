using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZISK.Services;
using ZISK.Shared.DTOs.Excuses;
using ExcuseStatus = ZISK.Shared.Enums.ExcuseStatus;

namespace ZISK.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExcusesController : ControllerBase
{
    private readonly IExcuseService _excuseService;

    public ExcusesController(IExcuseService excuseService)
    {
        _excuseService = excuseService;
    }

    [HttpGet]
    public async Task<ActionResult<List<ExcuseListDto>>> GetExcuses([FromQuery] ExcuseStatus? status = null)
    {
        var result = await _excuseService.GetExcusesAsync(status, User);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ExcuseDto>> GetExcuse(Guid id)
    {
        try
        {
            var result = await _excuseService.GetExcuseAsync(id, User);
            return Ok(result);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost]
    public async Task<ActionResult<ExcuseDto>> CreateExcuse([FromBody] CreateExcuseRequest request)
    {
        try
        {
            var result = await _excuseService.CreateExcuseAsync(request, User);
            return CreatedAtAction(nameof(GetExcuse), new { id = result.Id }, result);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateExcuseStatusRequest request)
    {
        try
        {
            await _excuseService.UpdateStatusAsync(id, request, User);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateExcuse(Guid id, [FromBody] UpdateExcuseRequest request)
    {
        try
        {
            await _excuseService.UpdateExcuseAsync(id, request, User);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteExcuse(Guid id)
    {
        try
        {
            await _excuseService.DeleteExcuseAsync(id, User);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("my")]
    public async Task<ActionResult<List<ExcuseListDto>>> GetMyExcuses()
    {
        try
        {
            var result = await _excuseService.GetMyExcusesAsync(User);
            return Ok(result);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("pending/count")]
    public async Task<ActionResult<int>> GetPendingCount()
    {
        try
        {
            var count = await _excuseService.GetPendingCountAsync(User);
            return Ok(count);
        }
        catch (UnauthorizedAccessException) { return Unauthorized(); }
    }
}
