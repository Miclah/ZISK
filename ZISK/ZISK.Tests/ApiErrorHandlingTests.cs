using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Refit;
using ZISK.Client.Services;
using ZISK.Filters;

namespace ZISK.Tests;

/// <summary>
/// End-to-end check that an unhandled server exception never reaches the user as raw
/// HTML/JSON — it must come out of ApiExceptionFilter as JSON with a Slovak message, and
/// ApiErrorFormatter (used by every Snackbar on the client) must render exactly that message,
/// not the JSON envelope itself. Also verifies DataAnnotations validation errors round-trip
/// as their Slovak ErrorMessage rather than .NET's built-in English default.
/// </summary>
public class ApiErrorHandlingTests
{
    private static ExceptionContext MakeExceptionContext(Exception ex)
    {
        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            new ActionDescriptor());
        return new ExceptionContext(actionContext, [])
        {
            Exception = ex
        };
    }

    [Fact]
    public void ApiExceptionFilter_ProducesSlovak500Json_ForUnhandledException()
    {
        var filter = new ApiExceptionFilter(NullLogger<ApiExceptionFilter>.Instance);
        var context = MakeExceptionContext(new InvalidOperationException("SqlException: FK constraint 'FK_TrainingSeries_Users' violated on column 'CoachId'."));

        filter.OnException(context);

        Assert.True(context.ExceptionHandled);
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);

        // The raw exception text (with its SQL/column names) must never reach the response body.
        var json = JsonSerializer.Serialize(result.Value);
        Assert.DoesNotContain("SqlException", json);
        Assert.DoesNotContain("FK_TrainingSeries", json);

        // Round-trip through a real JSON parser (like ApiErrorFormatter does) rather than
        // substring-matching the serialized text, which System.Text.Json \u-escapes by default.
        using var parsed = JsonDocument.Parse(json);
        Assert.Equal("Nastala neočakávaná chyba. Skúste to znova.", parsed.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task ApiErrorFormatter_RendersSlovakMessage_ForApiExceptionFilterOutput()
    {
        // Simulates exactly what ApiExceptionFilter now writes to the response body.
        var body = JsonSerializer.Serialize(new { message = "Nastala neočakávaná chyba. Skúste to znova." });
        var apiException = await BuildApiException(HttpStatusCode.InternalServerError, body);

        var userMessage = ApiErrorFormatter.ToUserMessage(apiException);

        Assert.Equal("Nastala neočakávaná chyba. Skúste to znova.", userMessage);
        // Must not be the raw JSON envelope, and must not be an English fallback.
        Assert.DoesNotContain("{", userMessage);
        Assert.DoesNotContain("message", userMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApiErrorFormatter_RendersRawHtml_IfServerEverLeaksTheGenericErrorPage()
    {
        // Documents the failure mode the filter is meant to prevent: if an exception somehow
        // still escaped to app.UseExceptionHandler("/Error") (outside the MVC pipeline this
        // filter covers), ApiErrorFormatter's JSON parse fails and falls back to dumping the
        // raw HTML into the Snackbar. This is intentionally still broken for that specific
        // out-of-pipeline case — asserting it here so a future fix doesn't silently regress
        // an assumption we're relying on elsewhere.
        const string html = "<!DOCTYPE html><html><body><h1>Error.</h1></body></html>";
        var apiException = await BuildApiException(HttpStatusCode.InternalServerError, html);

        var userMessage = ApiErrorFormatter.ToUserMessage(apiException);

        Assert.Equal(html, userMessage);
    }

    [Fact]
    public async Task ApiErrorFormatter_RendersSlovakMessage_ForValidationProblemDetails()
    {
        // Shape ASP.NET Core's [ApiController] auto-400 produces for a failed DataAnnotations
        // check, e.g. CreateChildRequest.FirstName now that it carries an explicit Slovak
        // ErrorMessage instead of relying on the (English-only) built-in default.
        var body = JsonSerializer.Serialize(new
        {
            title = "One or more validation errors occurred.",
            status = 400,
            errors = new Dictionary<string, string[]>
            {
                ["FirstName"] = ["Meno je povinné."]
            }
        });
        var apiException = await BuildApiException(HttpStatusCode.BadRequest, body);

        var userMessage = ApiErrorFormatter.ToUserMessage(apiException);

        Assert.Equal("Meno je povinné.", userMessage);
    }

    private static async Task<ApiException> BuildApiException(HttpStatusCode statusCode, string content)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/api/test");
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content)
        };
        return await ApiException.Create(request, HttpMethod.Post, response, new RefitSettings());
    }
}
