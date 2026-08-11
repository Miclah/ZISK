namespace ZISK.Middleware;

/// <summary>
/// Renders the same self-contained "not found" page for two different callers:
/// <see cref="DemoAccessGuardMiddleware"/> (a guarded path with no owner cookie) and the
/// server's catch-all fallback route (a URL that genuinely matches nothing). Both have to
/// produce an identical response - status 404, same body - or the difference between them would
/// itself reveal which guarded paths exist, defeating the point of guarding them.
///
/// Written as raw HTML rather than a Razor component because both callers sit outside the Blazor
/// render pipeline: the fallback route is a plain endpoint, not a page, and this middleware runs
/// before routing has even chosen an endpoint.
/// </summary>
public static class NotFoundResponseWriter
{
    public static async Task WriteAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";

        var lang = context.Request.Cookies.TryGetValue("zisk_lang", out var cookie) && cookie == "sk" ? "sk" : "en";
        await context.Response.WriteAsync(Page.Replace("__LANG__", lang));
    }

    private const string Page = """
<!DOCTYPE html>
<html lang="__LANG__">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>ZISK</title>
<style>
  :root {
    --zisk-primary: #1E3A5F;
    --zisk-accent: #D97706;
    --zisk-bg: #F1F5F9;
    --zisk-surface: #FFFFFF;
    --zisk-border: #E2E8F0;
    --zisk-text: #0F172A;
    --zisk-text-secondary: #64748B;
  }
  * { box-sizing: border-box; }
  body {
    margin: 0;
    min-height: 100vh;
    display: flex;
    align-items: center;
    justify-content: center;
    background: var(--zisk-bg);
    padding: clamp(2rem, 8vh, 4.5rem) 1.5rem;
    font-family: 'Segoe UI', Roboto, system-ui, -apple-system, sans-serif;
    -webkit-font-smoothing: antialiased;
    color: var(--zisk-text);
  }
  .panel {
    width: 100%;
    max-width: 38rem;
    background: var(--zisk-surface);
    border: 1px solid var(--zisk-border);
    border-radius: 12px;
    box-shadow: 0 4px 12px rgba(15, 23, 42, 0.1);
    overflow: hidden;
  }
  .bar {
    display: flex;
    align-items: center;
    gap: 0.625rem;
    height: 3.5rem;
    padding: 0 1.25rem;
    background: var(--zisk-primary);
  }
  .wordmark { font-size: 1.125rem; font-weight: 700; letter-spacing: -0.02em; color: #fff; }
  .badge {
    font-size: 0.6875rem; font-weight: 600; letter-spacing: 0.06em; color: #fff;
    background: var(--zisk-accent); border-radius: 999px; padding: 0.1875rem 0.5rem; line-height: 1;
  }
  .body { padding: 2rem 1.75rem 2.25rem; text-align: center; }
  h1 { margin: 0 0 0.625rem; font-size: 1.25rem; font-weight: 600; letter-spacing: -0.01em; }
  p { margin: 0; color: var(--zisk-text-secondary); font-size: 0.9375rem; line-height: 1.6; }
  a.home {
    display: inline-block; margin-top: 1.5rem; padding: 0.5rem 1.125rem;
    border: 1px solid var(--zisk-primary); border-radius: 8px;
    color: var(--zisk-primary); text-decoration: none; font-size: 0.875rem; font-weight: 600;
  }
  a.home:hover { background: var(--zisk-bg); }
</style>
</head>
<body>
  <main class="panel">
    <div class="bar">
      <span class="wordmark">ZISK</span>
    </div>
    <div class="body">
      <h1 data-sk="Stránka nenájdená" data-en="Page not found"></h1>
      <p data-sk="Požadovaná stránka neexistuje alebo bola presunutá." data-en="The page you're looking for doesn't exist or has moved."></p>
      <a class="home" href="/" data-sk="Domov" data-en="Home"></a>
    </div>
  </main>
<script>
(function () {
  var lang = document.documentElement.lang === 'sk' ? 'sk' : 'en';
  Array.prototype.forEach.call(document.querySelectorAll('[data-' + lang + ']'), function (el) {
    el.textContent = el.getAttribute('data-' + lang);
  });
})();
</script>
</body>
</html>
""";
}
