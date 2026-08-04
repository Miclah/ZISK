using Refit;
using ZISK.Shared.DTOs.Demo;

namespace ZISK.Client.Services;

public interface IDemoApi
{
    /// <summary>Anonymous-friendly - RedirectToLogin.razor calls this before deciding whether to send an unauthenticated visitor to /demo or /login.</summary>
    [Get("/api/demo/status")]
    Task<DemoStatusDto> GetStatusAsync();

    /// <summary>Wipes the caller's demo session data and re-clones a fresh copy. Signs the caller out (their own account was just replaced) - the client redirects to /demo afterwards.</summary>
    [Post("/api/demo/reset")]
    Task ResetAsync();
}
