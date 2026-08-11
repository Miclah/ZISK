using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using System.Globalization;
using System.IO.Compression;
using System.Threading.RateLimiting;
using ZISK.Components;
using ZISK.Components.Account;
using ZISK.Client.Services;
using ZISK.Data;
using ZISK.Extensions;
using ZISK.Filters;
using ZISK.Middleware;
using ZISK.Services;
using ZISK.Services.Demo;
using ZISK.Shared.Localization;

// English is the default the whole app now starts from, so server-rendered formatting has to match
// it. Slovak is still fully supported below, it just has to be asked for.
var englishCulture = new CultureInfo("en-GB");
var slovakCulture = new CultureInfo("sk-SK");
CultureInfo.DefaultThreadCurrentCulture = englishCulture;
CultureInfo.DefaultThreadCurrentUICulture = englishCulture;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
    {
        options.Filters.Add<ApiExceptionFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// DataAnnotations ErrorMessage strings on ZISK.Shared DTOs are translation keys, not literal
// text (see Translations.cs) - translate them into the requesting client's language (from the
// Accept-Language header AcceptLanguageHandler stamps on every client call) before the automatic
// 400 response goes out, instead of leaking raw keys like "validation.name.required" to the UI.
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var lang = context.HttpContext.RequestServices.GetRequiredService<ICurrentLanguage>().Current;
        var errors = context.ModelState
            .Where(kvp => kvp.Value?.Errors.Count > 0)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value!.Errors.Select(e => Translations.Get(lang, e.ErrorMessage)).ToArray());

        return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new { errors });
    };
});

// Razor Components
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents()
    .AddAuthenticationStateSerialization(options =>
    {
        options.SerializeAllClaims = true;
    });

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
})
    .AddIdentityCookies();

builder.Services.AddAuthorization();

// Registered before AddDbContext: ApplicationDbContext's constructor takes IDemoSessionContext,
// and AddDbContext below wires DemoStampingInterceptor in via the (sp, options) overload.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IDemoSessionContext, DemoSessionContext>();
builder.Services.AddScoped<DemoStampingInterceptor>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
    options.UseSqlServer(connectionString, sql =>
           {
               // The demo deployment runs on Azure SQL serverless with auto-pause. A paused
               // database refuses the first connection and only *then* starts resuming, which
               // takes the better part of a minute - without a retry policy every request that
               // lands on a cold database fails outright. The 120s command timeout covers the
               // same resume window for a query that gets through the handshake.
               sql.EnableRetryOnFailure(
                   maxRetryCount: 8,
                   maxRetryDelay: TimeSpan.FromSeconds(15),
                   errorNumbersToAdd: null);
               sql.CommandTimeout(120);
           })
           .AddInterceptors(sp.GetRequiredService<DemoStampingInterceptor>()));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Identity
builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddClaimsPrincipalFactory<CustomClaimsPrincipalFactory>()
    .AddSignInManager()
    .AddDefaultTokenProviders()
    .AddErrorDescriber<SlovakIdentityErrorDescriber>();

builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
    options.TokenLifespan = TimeSpan.FromHours(1));

// Fixed for the lifetime of the process - the seed mode comes from env/config read at startup.
var isDemoDeployment = SeedModeResolver.Resolve(builder.Configuration) == SeedMode.Demo;

// Cookie konfiguracia
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/access-denied";
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

    // In demo mode an anonymous visitor must land on /demo, never on /login (which
    // DemoAccessGuardMiddleware 404s without the owner cookie).
    //
    // This has to be handled here, not only in RedirectToLogin.razor, because [Authorize] on a
    // routable Blazor component is copied onto its endpoint metadata by MapRazorComponents. On the
    // very first request the app is still static SSR (App.razor hands out a null render mode), so
    // the authorization middleware challenges the endpoint and this cookie handler issues the 302
    // before Routes.razor renders - RedirectToLogin never runs on that path at all. It still runs
    // for client-side navigation once WASM is interactive, so both paths need the demo check.
    options.Events.OnRedirectToLogin = context =>
    {
        if (DemoAccessGuardMiddleware.ShouldRouteToDemoLanding(context.Request, builder.Configuration))
        {
            context.Response.Redirect("/demo");
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddTransient<SmtpEmailSender>();
builder.Services.AddTransient<LoggingEmailSender>();

// Demo deployments must never send real email - see LoggingEmailSender.
if (isDemoDeployment)
{
    builder.Services.AddTransient<IEmailSender<ApplicationUser>>(sp => sp.GetRequiredService<LoggingEmailSender>());
    builder.Services.AddTransient<IEmailSender>(sp => sp.GetRequiredService<LoggingEmailSender>());
}
else
{
    builder.Services.AddTransient<IEmailSender<ApplicationUser>>(sp => sp.GetRequiredService<SmtpEmailSender>());
    builder.Services.AddTransient<IEmailSender>(sp => sp.GetRequiredService<SmtpEmailSender>());
}
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<ITeamAccessService, TeamAccessService>();
builder.Services.Configure<SeedPasswordOptions>(builder.Configuration.GetSection("Seed:Passwords"));
builder.Services.Configure<SeedInitialAdminOptions>(builder.Configuration.GetSection("Seed:InitialAdmin"));
builder.Services.AddScoped<DatabaseInitializer>();
// Singleton, and registered before the workers below that await it: initialization now runs
// alongside them instead of before them. See DatabaseInitializationService for why it moved.
builder.Services.AddSingleton<StartupState>();
builder.Services.AddHostedService<DatabaseInitializationService>();
builder.Services.AddScoped<UsernameGenerator>();
builder.Services.AddScoped<RegistrationDraftService>();
builder.Services.AddHostedService<ChildUpgradeService>();
builder.Services.AddHostedService<TrainingSeriesGeneratorService>();
builder.Services.AddHostedService<AttendanceAutoCloseService>();
builder.Services.Configure<DemoOptions>(builder.Configuration.GetSection("Demo"));
builder.Services.AddScoped<IDemoSessionService, DemoSessionService>();
builder.Services.AddHostedService<DemoTemplateRefreshWorker>();
builder.Services.AddHostedService<DemoSessionCleanupWorker>();
builder.Services.AddTransient<ForwardAuthHeaderHandler>();


builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("forgot-password", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0
            }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

builder.Services.AddMudServices();
builder.Services.AddScoped<UserContextService>();
builder.Services.AddScoped<OnlineStatusService>();
builder.Services.AddSingleton<HttpActivityTracker>();
builder.Services.AddApplicationServices();
builder.Services.AddRefitClients(builder.Configuration);

builder.Services.Configure<Microsoft.AspNetCore.Builder.RequestLocalizationOptions>(options =>
{
    // Both, not just one. The list used to hold Slovak alone, which meant a visitor who switched to
    // English still got Slovak month and weekday names from anything the framework formatted.
    var supported = new[] { englishCulture, slovakCulture };
    options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(englishCulture);
    options.SupportedCultures = supported;
    options.SupportedUICultures = supported;
});

// MapStaticAssets serves pre-compressed WASM and static files, but controller JSON does not go
// through it, so API responses need compression of their own. The list endpoints return whole
// collections: at full club size /api/users is roughly 525 KB uncompressed and 90 KB compressed.
builder.Services.AddResponseCompression(options =>
{
    // Restricted to JSON: static assets already ship pre-compressed .br/.gz via MapStaticAssets, and
    // re-compressing them at runtime would be strictly worse (CPU per request, no size gain).
    options.MimeTypes = ["application/json", "application/problem+json"];

    // BREACH-style attacks need a secret reflected in a compressed response body alongside
    // attacker-controlled input. These endpoints return DTO collections; the antiforgery token lives in the
    // server-rendered Identity pages, which are not JSON and so are not compressed here.
    options.EnableForHttps = true;
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

var app = builder.Build();

// Azure App Service Linux terminates TLS at its own edge and forwards requests to the container over
// plain HTTP, setting X-Forwarded-Proto/-For. Without this, Request.IsHttps is always false behind that
// proxy, which would silently defeat the Secure cookie policy and HSTS below. KnownNetworks/KnownProxies
// are cleared because App Service's front-end IP isn't fixed/known in advance - the container itself is
// not directly internet-facing, so trusting these headers here doesn't introduce a spoofing risk.
// The docker-compose path (plain HTTP, no proxy) and local `dotnet run` are unaffected either way since
// no X-Forwarded-* headers are ever present there.
var forwardedHeaderOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeaderOptions.KnownIPNetworks.Clear();
forwardedHeaderOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeaderOptions);

// First real middleware in the pipeline, because everything after it assumes a usable database -
// UseAuthentication resolves the cookie against AspNetUsers on the very first request. Until
// DatabaseInitializationService reports ready this serves a self-contained waiting page instead.
app.UseMiddleware<StartupGateMiddleware>();

// Before the endpoints that produce the JSON it compresses, and after UseForwardedHeaders so the
// EnableForHttps decision sees the real client scheme rather than the proxy hop.
app.UseResponseCompression();

app.UseRequestLocalization();

// Before authentication: guarded paths (login, registration, the Identity scaffold) 404 for
// anyone without the owner cookie in demo mode, so there's no reason to run auth machinery for
// them at all. See DemoAccessGuardMiddleware for what this locks down and why.
app.UseMiddleware<DemoAccessGuardMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // Response-header-only (Strict-Transport-Security); does not redirect or refuse plain HTTP itself,
    // so it can't break the docker-compose deployment path, which never serves HTTPS at all.
    app.UseHsts();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();

app.MapControllers();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(ZISK.Client._Imports).Assembly);

app.MapAdditionalIdentityEndpoints();

// Anonymous by construction, mapped after StartupGateMiddleware bypasses them - external monitors
// need a trivial, always-reachable answer, not a page that depends on auth or the database.
app.MapGet("/health", () => Results.Ok());

// A plain ApplicationDbContext would inherit its EnableRetryOnFailure policy and 120s command
// timeout, so a paused demo database could hold a health check open for minutes. This connects
// directly instead, with its own short timeout and no retries, so a down database fails the check
// in seconds rather than hanging it.
app.MapGet("/health/db", async (IConfiguration configuration, CancellationToken cancellationToken) =>
{
    var connectionString = configuration.GetConnectionString("DefaultConnection");
    await using var connection = new SqlConnection(connectionString);
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    timeout.CancelAfter(TimeSpan.FromSeconds(5));

    try
    {
        await connection.OpenAsync(timeout.Token);
        return Results.Ok();
    }
    catch (Exception)
    {
        return Results.StatusCode(503);
    }
});

// Database migration and seeding used to run here, between the mapping above and app.Run() below.
// That blocked Kestrel from binding a port until it finished, which on the demo deployment meant a
// browser waiting out the whole Azure SQL serverless resume on a request that had not returned a
// single header. It now runs as DatabaseInitializationService, with StartupGateMiddleware holding
// requests until it reports ready.

app.Run();