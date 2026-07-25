using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZISK.Services;
using ZISK.Shared.DTOs.Seasons;

namespace ZISK.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SeasonsController : ControllerBase
{
    private readonly ISeasonService _seasonService;

    public SeasonsController(ISeasonService seasonService)
    {
        _seasonService = seasonService;
    }

    [HttpGet]
    public async Task<ActionResult<List<SeasonDto>>> GetSeasons()
    {
        var result = await _seasonService.GetSeasonsAsync();
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SeasonDto>> GetSeason(Guid id)
    {
        try
        {
            var result = await _seasonService.GetSeasonAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SeasonDto>> CreateSeason([FromBody] CreateSeasonRequest request)
    {
        try
        {
            var result = await _seasonService.CreateSeasonAsync(request);
            return CreatedAtAction(nameof(GetSeason), new { id = result.Id }, result);
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SeasonDto>> UpdateSeason(Guid id, [FromBody] UpdateSeasonRequest request)
    {
        try
        {
            var result = await _seasonService.UpdateSeasonAsync(id, request);
            return Ok(result);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteSeason(Guid id)
    {
        try
        {
            await _seasonService.DeleteSeasonAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("{id:guid}/activate")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SeasonDto>> Activate(Guid id)
    {
        try
        {
            var result = await _seasonService.ActivateAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
