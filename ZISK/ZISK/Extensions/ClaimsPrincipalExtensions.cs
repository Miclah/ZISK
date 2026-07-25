using System.Security.Claims;

namespace ZISK.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static string? GetUserId(this ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.NameIdentifier);

    // Throws UnauthorizedAccessException (not InvalidOperationException) intentionally
    // the global exception handler can catch it and return a 401 response without any extra logic in the caller.
    public static string GetRequiredUserId(this ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? throw new UnauthorizedAccessException("User is not authenticated.");

    public static string? GetEmail(this ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.Email);
}
