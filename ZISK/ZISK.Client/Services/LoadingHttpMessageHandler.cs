using System.Net;
using Microsoft.AspNetCore.Components;

namespace ZISK.Client.Services;

public sealed class LoadingHttpMessageHandler : DelegatingHandler
{
    private readonly HttpActivityTracker _tracker;
    private readonly NavigationManager _navigation;

    public LoadingHttpMessageHandler(HttpActivityTracker tracker, NavigationManager navigation)
    {
        _tracker = tracker;
        _navigation = navigation;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _tracker.Begin();
        try
        {
            var response = await base.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized && !IsAuthEndpoint(request))
            {
                var currentUri = new Uri(_navigation.Uri);
                var currentPath = currentUri.PathAndQuery;
                if (!currentPath.StartsWith("/auth", StringComparison.OrdinalIgnoreCase) &&
                    !currentPath.StartsWith("/login", StringComparison.OrdinalIgnoreCase))
                {
                    var returnUrl = Uri.EscapeDataString(currentPath);
                    _navigation.NavigateTo($"/auth?returnUrl={returnUrl}", forceLoad: false);
                }
            }

            return response;
        }
        finally
        {
            _tracker.End();
        }
    }

    private static bool IsAuthEndpoint(HttpRequestMessage request)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        return path.Contains("/login", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/logout", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/register", StringComparison.OrdinalIgnoreCase);
    }
}
