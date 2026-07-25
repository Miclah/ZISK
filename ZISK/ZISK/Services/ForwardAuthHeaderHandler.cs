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
}
