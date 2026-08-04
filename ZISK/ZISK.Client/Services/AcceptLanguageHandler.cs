using ZISK.Shared.Localization;

namespace ZISK.Client.Services;

/// <summary>
/// Stamps every API request with the visitor's current UI language so the server can translate
/// DataAnnotations validation-key messages and Services-layer error messages into the same
/// language the request came from. Registered on all Refit clients alongside
/// LoadingHttpMessageHandler; see ILanguageService for how Current is tracked.
/// </summary>
public sealed class AcceptLanguageHandler : DelegatingHandler
{
    private readonly ILanguageService _languageService;

    public AcceptLanguageHandler(ILanguageService languageService)
    {
        _languageService = languageService;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Remove("Accept-Language");
        request.Headers.Add("Accept-Language", _languageService.Current == Lang.En ? "en" : "sk");
        return base.SendAsync(request, cancellationToken);
    }
}
