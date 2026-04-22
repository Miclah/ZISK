using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZISK.Services;
using ZISK.Shared.DTOs.Addresses;

namespace ZISK.Controllers;

[ApiController]
[Route("api/addresses")]
[AllowAnonymous]
public class NominatimController : ControllerBase
{
    private readonly NominatimService _service;

    public NominatimController(NominatimService service)
    {
        _service = service;
    }

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<NominatimAddressDto>>> Search(
        [FromQuery] string q,
        CancellationToken ct)
    {
        var results = await _service.SearchAsync(q ?? string.Empty, ct);
        return Ok(results);
    }
}
