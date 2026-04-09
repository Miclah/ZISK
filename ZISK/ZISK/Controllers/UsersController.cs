using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZISK.Services;
using ZISK.Shared.DTOs.Users;

namespace ZISK.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<ActionResult<List<UserListDto>>> GetUsers([FromQuery] string? role = null)
    {
        var result = await _userService.GetUsersAsync(role);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetUser(string id)
    {
        try
        {
            var result = await _userService.GetUserAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("parents")]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<ActionResult<List<ParentOptionDto>>> GetParents()
    {
        var result = await _userService.GetParentsAsync();
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> CreateUser(CreateUserRequest request)
    {
        try
        {
            var result = await _userService.CreateUserAsync(request, User);
            return Ok(result);
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<UserDto>> UpdateUser(string id, UpdateUserRequest request)
    {
        try
        {
            var result = await _userService.UpdateUserAsync(id, request, User);
            return Ok(result);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return StatusCode(500, ex.Message); }
    }

    [HttpPost("{id}/toggle-status")]
    public async Task<IActionResult> ToggleStatus(string id)
    {
        try
        {
            await _userService.ToggleStatusAsync(id, User);
            return Ok();
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("coaches")]
    [Authorize(Roles = "Admin,Coach")]
    public async Task<ActionResult<List<UserListDto>>> GetCoaches()
    {
        var result = await _userService.GetCoachesAsync();
        return Ok(result);
    }

    [HttpPost("{userId}/teams/{teamId}")]
    public async Task<IActionResult> AssignTeam(string userId, Guid teamId, [FromQuery] bool isPrimary = false)
    {
        try
        {
            await _userService.AssignTeamAsync(userId, teamId, isPrimary);
            return Ok();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Databáza")) { return StatusCode(500, ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpDelete("{userId}/teams/{teamId}")]
    public async Task<IActionResult> RemoveTeam(string userId, Guid teamId)
    {
        try
        {
            await _userService.RemoveTeamAsync(userId, teamId);
            return Ok();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return StatusCode(500, ex.Message); }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        try
        {
            await _userService.DeleteUserAsync(id, User);
            return Ok();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }
}
