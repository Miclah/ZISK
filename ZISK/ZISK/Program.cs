using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using System.Globalization;
using System.Threading.RateLimiting;
using ZISK.Components;
using ZISK.Components.Account;
using ZISK.Client.Services;
using ZISK.Data;
using ZISK.Extensions;
using ZISK.Services;

var slovakCulture = new CultureInfo("sk-SK");
CultureInfo.DefaultThreadCurrentCulture = slovakCulture;
CultureInfo.DefaultThreadCurrentUICulture = slovakCulture;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
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
builder.Services.AddTransient<IEmailSender<ApplicationUser>>(sp => sp.GetRequiredService<SmtpEmailSender>());
builder.Services.AddTransient<IEmailSender>(sp => sp.GetRequiredService<SmtpEmailSender>());
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

var app = builder.Build();

app.UseRequestLocalization();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
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