using System.Text.Json;
using ZISK.Services;

namespace ZISK.Middleware;

/// <summary>
/// Holds every request until <see cref="DatabaseInitializer"/> has finished, serving a self-contained
/// "waking up" page in the meantime.
///
/// This is the other half of moving initialization off the startup path (see
/// <see cref="DatabaseInitializationService"/>). Kestrel now answers within seconds of the container
/// starting, so instead of a browser hanging on a headerless request for the length of an Azure SQL
/// serverless resume, the visitor gets a real page that says what is happening and refreshes itself
/// when the app is ready. It also preserves the invariant the old blocking startup gave us for free:
/// no request ever reaches the app while the database has no schema.
/// </summary>
public sealed class StartupGateMiddleware
{
    /// <summary>Polled by the waiting page. Anonymous by construction - this middleware answers it before auth runs.</summary>
    public const string StatusPath = "/api/startup-status";

    private readonly RequestDelegate _next;
    private readonly StartupState _state;

    public StartupGateMiddleware(RequestDelegate next, StartupState state)
    {
        _next = next;
        _state = state;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // External monitors need an unconditional answer, not the waiting page - health checks must
        // reach the endpoint even while the database is still coming up.
        if (context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var isStatusRequest = context.Request.Path.Equals(StatusPath, StringComparison.OrdinalIgnoreCase);

        if (_state.IsReady && !isStatusRequest)
        {
            await _next(context);
            return;
        }

        if (isStatusRequest)
        {
            await WriteStatusAsync(context);
            return;
        }

        // An API caller is a script, not a person - it wants a status code it can branch on, not an
        // HTML page it would fail to deserialize. This is the SPA already running in someone's tab
        // while the server restarts underneath it.
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers.RetryAfter = "10";
            await WriteStatusAsync(context);
            return;
        }

        // Deliberately 200 rather than 503. App Service decides a Linux container has started by
        // making an HTTP request to it, and a demo that trades a blank screen for a wrestling match
        // with the platform's start detection would be a bad trade. Nothing here is cacheable anyway.
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";

        // English unless the visitor has already chosen otherwise, which is the opposite of the app's
        // Slovak default. Most people who reach this screen are seeing the project for the first time
        // and got here from a link that was not in Slovak; the toggle is there for everyone else.
        var lang = context.Request.Cookies.TryGetValue("zisk_lang", out var cookie) && cookie == "sk" ? "sk" : "en";
        await context.Response.WriteAsync(Page.Replace("__LANG__", lang));
    }

    private async Task WriteStatusAsync(HttpContext context)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";

        // The phase name is safe to expose; StartupState.Error is not. It carries whatever the
        // provider threw, which for a connection failure includes the server host and database name.
        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            ready = _state.IsReady,
            failed = _state.Phase == StartupPhase.Failed,
            phase = _state.Phase.ToString(),
            elapsedSeconds = (int)_state.Elapsed.TotalSeconds
        }));
    }

    /// <summary>
    /// Fully self-contained on purpose: no stylesheet, no font, no script file, no MudBlazor. This
    /// page's whole job is to render on the one request that arrives before the app can serve
    /// anything, so every extra dependency is another way for it to show up unstyled.
    /// </summary>
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
    /* System stack, not Inter: a webfont here would be one more request on the slowest load the
       app ever serves, and it would arrive after the text it is meant to style. */
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
  /* Same position and weights as the demo landing's switch, so the two read as one product. */
  .lang { display: flex; margin-left: auto; gap: 0.75rem; font-size: 0.8125rem; font-weight: 500; }
  .lang button {
    background: none; border: 0; padding: 0; cursor: pointer; font: inherit;
    color: rgba(255, 255, 255, 0.55); letter-spacing: 0.04em;
  }
  .lang button:hover { color: rgba(255, 255, 255, 0.85); }
  .lang button[aria-pressed="true"] { color: #fff; }
  .body { padding: 2rem 1.75rem 2.25rem; }
  h1 { margin: 0 0 0.625rem; font-size: 1.25rem; font-weight: 600; letter-spacing: -0.01em; }
  p { margin: 0; color: var(--zisk-text-secondary); font-size: 0.9375rem; line-height: 1.6; }
  .track {
    position: relative; height: 4px; margin: 1.75rem 0 0.875rem;
    background: var(--zisk-border); border-radius: 999px; overflow: hidden;
  }
  .track > i {
    position: absolute; inset: 0 auto 0 0; width: 35%;
    background: var(--zisk-primary); border-radius: 999px;
    animation: slide 1.6s ease-in-out infinite;
  }
  @keyframes slide {
    0%   { left: -35%; }
    100% { left: 100%; }
  }
  .status {
    display: flex; justify-content: space-between; gap: 1rem;
    font-size: 0.8125rem; color: var(--zisk-text-secondary);
  }
  .elapsed { font-variant-numeric: tabular-nums; }
  /* Failure is a colour change and a different sentence, not a new layout. */
  body.failed .track > i { animation: none; left: 0; width: 100%; background: #DC2626; }
  @media (prefers-reduced-motion: reduce) {
    .track > i { animation: none; left: 0; width: 100%; opacity: 0.35; }
  }
</style>
</head>
<body>
  <main class="panel">
    <div class="bar">
      <span class="wordmark">ZISK</span><span class="badge">DEMO</span>
      <nav class="lang" aria-label="Language">
        <button type="button" data-lang="sk">SK</button>
        <button type="button" data-lang="en">EN</button>
      </nav>
    </div>
    <div class="body">
      <h1 id="heading"></h1>
      <p id="lead"></p>
      <div class="track" role="progressbar" aria-labelledby="heading"><i></i></div>
      <div class="status">
        <span id="phase" aria-live="polite"></span>
        <span class="elapsed" id="elapsed"></span>
      </div>
    </div>
  </main>
<script>
(function () {
  var STRINGS = {
    sk: {
      heading: 'Spúšťam demo',
      lead: 'Databáza sa prebúdza. Keď ju chvíľu nikto nepoužíva, uspí sa, a prvé načítanie ju musí zobudiť. Ďalšie už budú rýchle.',
      failedHeading: 'Demo sa zatiaľ nespustilo',
      failedLead: 'Databáza sa neozýva dlhšie, než je obvyklé. Skúšame ďalej a stránka sa obnoví sama, len čo bude pripravená.',
      WakingDatabase: 'Prebúdzam databázu', Migrating: 'Pripravujem databázu',
      Seeding: 'Napĺňam ukážkové dáta', Ready: 'Hotovo', Failed: 'Skúšam znova',
      elapsed: function (s) { return s + ' s'; }
    },
    en: {
      heading: 'Starting the demo',
      lead: 'The database is waking up. It goes to sleep when nobody has used it for a while, so the first load has to wake it up. Later ones are quick.',
      failedHeading: 'The demo has not started yet',
      failedLead: 'The database is taking longer to respond than usual. We are still retrying and this page will refresh itself as soon as it is ready.',
      WakingDatabase: 'Waking the database', Migrating: 'Preparing the database',
      Seeding: 'Loading sample data', Ready: 'Done', Failed: 'Retrying',
      elapsed: function (s) { return s + 's'; }
    }
  };

  var lang = document.documentElement.lang === 'sk' ? 'sk' : 'en';
  var t = STRINGS[lang];

  var heading = document.getElementById('heading');
  var lead = document.getElementById('lead');
  var phaseEl = document.getElementById('phase');
  var elapsedEl = document.getElementById('elapsed');

  var failed = false;
  var phase = 'WakingDatabase';
  // Seconds are counted here rather than taken from each poll response. Reading them off the
  // network made the display jump by however long the gap between polls was; the server value is
  // still authoritative and resyncs this on every response.
  var seconds = 0;

  function paint() {
    heading.textContent = failed ? t.failedHeading : t.heading;
    lead.textContent = failed ? t.failedLead : t.lead;
    phaseEl.textContent = t[phase] || '';
    elapsedEl.textContent = t.elapsed(seconds);
  }

  function setLang(next) {
    lang = next;
    t = STRINGS[next];
    document.documentElement.lang = next;
    // Shared with the rest of the app, so a choice made here carries into the app it loads into.
    document.cookie = 'zisk_lang=' + next + ';path=/;max-age=31536000;samesite=lax';
    Array.prototype.forEach.call(document.querySelectorAll('.lang button'), function (b) {
      b.setAttribute('aria-pressed', String(b.getAttribute('data-lang') === next));
    });
    paint();
  }

  Array.prototype.forEach.call(document.querySelectorAll('.lang button'), function (b) {
    b.addEventListener('click', function () { setLang(b.getAttribute('data-lang')); });
  });

  setInterval(function () { seconds += 1; elapsedEl.textContent = t.elapsed(seconds); }, 1000);

  function poll() {
    fetch('/api/startup-status', { cache: 'no-store' })
      .then(function (r) { return r.json(); })
      .then(function (s) {
        if (s.ready) { window.location.reload(); return; }
        failed = s.failed;
        phase = s.phase;
        seconds = s.elapsedSeconds;
        document.body.classList.toggle('failed', failed);
        paint();
        setTimeout(poll, 2000);
      })
      // A failed poll means the server went away again (App Service recycling the container is the
      // usual reason). Keep trying rather than freezing on a stale phase.
      .catch(function () { setTimeout(poll, 3000); });
  }

  Array.prototype.forEach.call(document.querySelectorAll('.lang button'), function (b) {
    b.setAttribute('aria-pressed', String(b.getAttribute('data-lang') === lang));
  });
  paint();
  poll();
})();
</script>
</body>
</html>
""";
}
