using System.Net.Http.Headers;

namespace ZISK.Services;

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
