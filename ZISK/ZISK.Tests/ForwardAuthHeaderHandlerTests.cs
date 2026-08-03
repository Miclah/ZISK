using Microsoft.AspNetCore.Http;
using ZISK.Services;

namespace ZISK.Tests;

/// <summary>
/// The server-side Refit clients call this app's own API. Their BaseAddress comes from
/// configuration and defaults to http://localhost:5224, so anywhere the app is not on that exact
/// port - the demo verification run on 5299, a container, App Service - every SSR self-call went
/// to a port nothing was listening on. The calling components catch and swallow that, so it
/// surfaced as silently missing data rather than an error.
///
/// ForwardAuthHeaderHandler now retargets these to the host the current request arrived on.
/// </summary>
public class ForwardAuthHeaderHandlerTests
{
    /// <summary>Captures the request it is asked to send instead of putting it on the wire.</summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Captured { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Captured = request;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }

    private static async Task<HttpRequestMessage> SendThroughHandlerAsync(
        HttpContext? httpContext,
        string requestUri)
    {
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var capturing = new CapturingHandler();
        var handler = new ForwardAuthHeaderHandler(accessor) { InnerHandler = capturing };

        using var invoker = new HttpMessageInvoker(handler);
        await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, requestUri), CancellationToken.None);

        Assert.NotNull(capturing.Captured);
        return capturing.Captured!;
    }

    private static DefaultHttpContext ContextOn(string scheme, string host)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = scheme;
        context.Request.Host = new HostString(host);
        return context;
    }

    [Fact]
    public async Task RewritesSelfCallToThePortTheRequestArrivedOn()
    {
        var sent = await SendThroughHandlerAsync(
            ContextOn("http", "localhost:5299"),
            "http://localhost:5224/api/demo/status");

        Assert.Equal("http://localhost:5299/api/demo/status", sent.RequestUri!.ToString());
    }

    [Fact]
    public async Task KeepsPathAndQueryIntact()
    {
        var sent = await SendThroughHandlerAsync(
            ContextOn("http", "localhost:5299"),
            "http://localhost:5224/api/trainings?teamId=42&upcoming=true");

        Assert.Equal("/api/trainings", sent.RequestUri!.AbsolutePath);
        Assert.Equal("?teamId=42&upcoming=true", sent.RequestUri.Query);
    }

    [Fact]
    public async Task FollowsSchemeAndHostSoItWorksBehindTheAzureProxy()
    {
        // UseForwardedHeaders has already rewritten Scheme/Host by the time this runs.
        var sent = await SendThroughHandlerAsync(
            ContextOn("https", "zisk.example.com"),
            "http://localhost:5224/api/me");

        Assert.Equal("https://zisk.example.com/api/me", sent.RequestUri!.ToString());
    }

    [Fact]
    public async Task LeavesTheUriAloneWhenThereIsNoRequestToCopyFrom()
    {
        // Background work has no HttpContext; the configured BaseAddress is all there is.
        var sent = await SendThroughHandlerAsync(
            httpContext: null,
            "http://localhost:5224/api/demo/status");

        Assert.Equal("http://localhost:5224/api/demo/status", sent.RequestUri!.ToString());
    }

    [Fact]
    public async Task ForwardsTheAuthCookieSoTheSelfCallIsNotAnonymous()
    {
        var context = ContextOn("http", "localhost:5299");
        context.Request.Headers["Cookie"] = ".AspNetCore.Identity.Application=abc123";

        var sent = await SendThroughHandlerAsync(context, "http://localhost:5224/api/me");

        Assert.True(sent.Headers.TryGetValues("Cookie", out var cookies));
        Assert.Equal(".AspNetCore.Identity.Application=abc123", cookies!.Single());
    }
}
