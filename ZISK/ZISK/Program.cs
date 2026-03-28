using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using Refit;
using ZISK.Client.Services;
using ZISK.Components;
using ZISK.Components.Account;
using ZISK.Data;
using ZISK.Services;

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
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 4;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddClaimsPrincipalFactory<CustomClaimsPrincipalFactory>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

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
builder.Services.AddTransient<IEmailSender<ApplicationUser>, SmtpEmailSender>();
builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<ITeamAccessService, TeamAccessService>();
builder.Services.AddScoped<DatabaseInitializer>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<ForwardAuthHeaderHandler>();

builder.Services.AddMudServices();

var baseAddress = new Uri("http://localhost:5224");
builder.Services.AddRefitClient<IExcusesApi>()
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<ForwardAuthHeaderHandler>();
builder.Services.AddRefitClient<IAttendanceApi>()
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<ForwardAuthHeaderHandler>();
builder.Services.AddRefitClient<ITrainingsApi>()
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<ForwardAuthHeaderHandler>();
builder.Services.AddRefitClient<ITeamsApi>()
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<ForwardAuthHeaderHandler>();
builder.Services.AddRefitClient<IAnnouncementsApi>()
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<ForwardAuthHeaderHandler>();
builder.Services.AddRefitClient<IDocumentsApi>()
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<ForwardAuthHeaderHandler>();
builder.Services.AddRefitClient<IChildrenApi>()
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<ForwardAuthHeaderHandler>();
builder.Services.AddRefitClient<IUsersApi>()
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<ForwardAuthHeaderHandler>();
builder.Services.AddRefitClient<IStatsApi>()
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<ForwardAuthHeaderHandler>();

var app = builder.Build();

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
    catch (Exception ex)
    {
        logger.LogError(ex, "Database initialization failed. Application will continue startup, but some seeded data may be missing.");
    }
}

app.Run();