using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Refit;
using ZISK.Client.Services;

var slovakCulture = new CultureInfo("sk-SK");
CultureInfo.DefaultThreadCurrentCulture = slovakCulture;
CultureInfo.DefaultThreadCurrentUICulture = slovakCulture;
CultureInfo.CurrentCulture = slovakCulture;
CultureInfo.CurrentUICulture = slovakCulture;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddMudServices(config =>
{
    // Snackbars carry the reason an action was refused (why a training cannot be deleted, for
    // example). At the previous 1000 ms they were gone before that sentence could be read.
    config.SnackbarConfiguration.VisibleStateDuration = 8000;
    config.SnackbarConfiguration.ShowTransitionDuration = 100;
    config.SnackbarConfiguration.HideTransitionDuration = 300;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.PreventDuplicates = false;
});
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateDeserialization();
builder.Services.AddScoped<UserContextService>();
builder.Services.AddScoped<OnlineStatusService>();
builder.Services.AddSingleton<HttpActivityTracker>();
builder.Services.AddSingleton<ILanguageService, LanguageService>();
builder.Services.AddTransient<LoadingHttpMessageHandler>();
builder.Services.AddTransient<AcceptLanguageHandler>();

var baseAddress = new Uri(builder.HostEnvironment.BaseAddress);

var refitSettings = new RefitSettings
{
    ContentSerializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    })
};

// One registration per API surface. Each client shares the same JSON settings and the same
// two handlers: LoadingHttpMessageHandler drives the global progress bar, AcceptLanguageHandler
// tells the server which language to return error messages in.
builder.Services.AddRefitClient<IExcusesApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<LoadingHttpMessageHandler>()
    .AddHttpMessageHandler<AcceptLanguageHandler>();

builder.Services.AddRefitClient<IAttendanceApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<LoadingHttpMessageHandler>()
    .AddHttpMessageHandler<AcceptLanguageHandler>();

builder.Services.AddRefitClient<ITrainingsApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<LoadingHttpMessageHandler>()
    .AddHttpMessageHandler<AcceptLanguageHandler>();

builder.Services.AddRefitClient<ITeamsApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<LoadingHttpMessageHandler>()
    .AddHttpMessageHandler<AcceptLanguageHandler>();

builder.Services.AddRefitClient<IAnnouncementsApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<LoadingHttpMessageHandler>()
    .AddHttpMessageHandler<AcceptLanguageHandler>();

builder.Services.AddRefitClient<IDocumentsApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<LoadingHttpMessageHandler>()
    .AddHttpMessageHandler<AcceptLanguageHandler>();

builder.Services.AddRefitClient<IChildrenApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<LoadingHttpMessageHandler>()
    .AddHttpMessageHandler<AcceptLanguageHandler>();

builder.Services.AddRefitClient<IUsersApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<LoadingHttpMessageHandler>()
    .AddHttpMessageHandler<AcceptLanguageHandler>();

builder.Services.AddRefitClient<IMeApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<LoadingHttpMessageHandler>()
    .AddHttpMessageHandler<AcceptLanguageHandler>();

builder.Services.AddRefitClient<IStatsApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<LoadingHttpMessageHandler>()
    .AddHttpMessageHandler<AcceptLanguageHandler>();

builder.Services.AddRefitClient<IInvitationsApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<LoadingHttpMessageHandler>()
    .AddHttpMessageHandler<AcceptLanguageHandler>();

builder.Services.AddRefitClient<ISeasonsApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<LoadingHttpMessageHandler>()
    .AddHttpMessageHandler<AcceptLanguageHandler>();

builder.Services.AddRefitClient<ITrainingSeriesApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<LoadingHttpMessageHandler>()
    .AddHttpMessageHandler<AcceptLanguageHandler>();

builder.Services.AddRefitClient<IDemoApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<LoadingHttpMessageHandler>()
    .AddHttpMessageHandler<AcceptLanguageHandler>();

var host = builder.Build();

// Reads the persisted zisk_lang cookie before the app renders, so pages don't flash Slovak
// and then re-render in English a frame later for a returning English-preference visitor.
await host.Services.GetRequiredService<ILanguageService>().InitializeAsync();

await host.RunAsync();
