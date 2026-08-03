using System.Net.Http.Headers;

namespace ZISK.Services;

// Blazor SSR runs on the server and calls its own REST API via HttpClient.
// Without this handler, those outgoing requests carry no auth cookie and the API returns 401.
// This handler copies the Cookie and Authorization headers from the incoming Blazor HTTP request to every outgoing API call.
public class ForwardAuthHeaderHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ForwardAuthHeaderHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is not null)
        {
            RetargetToCurrentHost(request, context);

            if (context.Request.Headers.TryGetValue("Cookie", out var cookieValue))
            {
                request.Headers.TryAddWithoutValidation("Cookie", cookieValue.ToString());
            }

            if (context.Request.Headers.TryGetValue("Authorization", out var authValue)
                && AuthenticationHeaderValue.TryParse(authValue.ToString(), out var authHeader))
            {
                request.Headers.Authorization = authHeader;
            }
        }

        return base.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Points the self-call at the host this process is actually serving, instead of whatever
    /// BaseAddress was configured at startup.
    ///
    /// These clients call the app's own API, so the correct target is always "wherever this
    /// request came in". The configured ApiBaseAddress cannot know that: it defaults to
    /// http://localhost:5224, so running on any other port (e.g. the demo verification run on
    /// 5299) sent every SSR self-call to a closed port - ECONNREFUSED, which the calling
    /// component swallows and renders as "no data" rather than an error. In a container or on
    /// App Service, where nothing listens on 5224 at all, every one of them would fail the same
    /// silent way.
    ///
    /// Only applies when there is an HttpContext, which is exactly the SSR self-call case. The
    /// configured BaseAddress still applies to anything calling these clients outside a request.
    /// </summary>
    private static void RetargetToCurrentHost(HttpRequestMessage request, HttpContext context)
    {
        if (request.RequestUri is null || !context.Request.Host.HasValue)
            return;

        request.RequestUri = new UriBuilder(request.RequestUri)
        {
            Scheme = context.Request.Scheme,
            Host = context.Request.Host.Host,
            Port = context.Request.Host.Port ?? (context.Request.IsHttps ? 443 : 80)
        }.Uri;
    }
}
