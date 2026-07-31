using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
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
using ZISK.Services;

var slovakCulture = new CultureInfo("sk-SK");
CultureInfo.DefaultThreadCurrentCulture = slovakCulture;
CultureInfo.DefaultThreadCurrentUICulture = slovakCulture;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
    {
        options.Filters.Add<ApiExceptionFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
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

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
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
});

builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddTransient<SmtpEmailSender>();
builder.Services.AddTransient<LoggingEmailSender>();

// Demo deployments must never send real email - see LoggingEmailSender.
if (SeedModeResolver.Resolve(builder.Configuration) == SeedMode.Demo)
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
builder.Services.AddScoped<UsernameGenerator>();
builder.Services.AddScoped<RegistrationDraftService>();
builder.Services.AddHostedService<ChildUpgradeService>();
builder.Services.AddHostedService<TrainingSeriesGeneratorService>();
builder.Services.AddHostedService<AttendanceAutoCloseService>();
builder.Services.AddHttpContextAccessor();
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
    var supported = new[] { slovakCulture };
    options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(slovakCulture);
    options.SupportedCultures = supported;
    options.SupportedUICultures = supported;
});

// API responses were previously sent uncompressed: MapStaticAssets serves the pre-compressed WASM/static
// assets, but controller JSON never passed through any compression middleware. The list endpoints return
// whole collections, so this is the difference between ~525 KB and ~90 KB on /api/users at full club size.
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

// Before the endpoints that produce the JSON it compresses, and after UseForwardedHeaders so the
// EnableForHttps decision sees the real client scheme rather than the proxy hop.
app.UseResponseCompression();

app.UseRequestLocalization();

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

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    try
    {
        await initializer.InitializeAsync();
    }
    catch (SeedConfigurationException)
    {
        // A demo/production deploy with missing Seed:* configuration must fail loudly at
        // startup, not silently fall back to hardcoded local passwords or run with no admin.
        throw;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database initialization failed. Application will continue startup, but some seeded data may be missing.");
    }
}

app.Run();