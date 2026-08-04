using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ZISK.Services;
using ZISK.Shared.Localization;

namespace ZISK.Filters;

// Safety net for API controllers: every controller already catches the exception types its
// service layer documents (KeyNotFoundException, ArgumentException, UnauthorizedAccessException,
// InvalidOperationException) and maps them to a proper JSON response. Anything else — e.g. a raw
// DbUpdateException from an unguarded FK/unique constraint — would otherwise fall through to
// app.UseExceptionHandler("/Error"), which renders an English HTML page. Refit callers can't parse
// that as JSON, so ApiErrorFormatter ends up dumping the raw HTML into a Snackbar. This filter
// catches anything unhandled before it gets that far and returns a JSON 500 instead.
public class ApiExceptionFilter : IExceptionFilter
{
    private readonly ILogger<ApiExceptionFilter> _logger;
    private readonly ICurrentLanguage _currentLanguage;

    public ApiExceptionFilter(ILogger<ApiExceptionFilter> logger, ICurrentLanguage currentLanguage)
    {
        _logger = logger;
        _currentLanguage = currentLanguage;
    }

    public void OnException(ExceptionContext context)
    {
        _logger.LogError(context.Exception, "Unhandled exception in {Path}", context.HttpContext.Request.Path);

        context.Result = new ObjectResult(new { message = Translations.Get(_currentLanguage.Current, "errors.unexpected") })
        {
            StatusCode = StatusCodes.Status500InternalServerError
        };
        context.ExceptionHandled = true;
    }
}
