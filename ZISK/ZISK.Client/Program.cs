using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Refit;
using ZISK.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddMudServices();
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateDeserialization();
builder.Services.AddScoped<UserContextService>();
builder.Services.AddScoped<OnlineStatusService>();
builder.Services.AddSingleton<HttpActivityTracker>();
builder.Services.AddTransient<LoadingHttpMessageHandler>();

var baseAddress = new Uri(builder.HostEnvironment.BaseAddress);

var refitSettings = new RefitSettings
{
    ContentSerializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    })
};

// Pomoc s AI pri robeni Refit klientov
builder.Services.AddRefitClient<IExcusesApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<LoadingHttpMessageHandler>();

builder.Services.AddRefitClient<IAttendanceApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<LoadingHttpMessageHandler>();

builder.Services.AddRefitClient<ITrainingsApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<LoadingHttpMessageHandler>();

builder.Services.AddRefitClient<ITeamsApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<LoadingHttpMessageHandler>();

builder.Services.AddRefitClient<IAnnouncementsApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<LoadingHttpMessageHandler>();

builder.Services.AddRefitClient<IDocumentsApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<LoadingHttpMessageHandler>();

builder.Services.AddRefitClient<IChildrenApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<LoadingHttpMessageHandler>();

builder.Services.AddRefitClient<IUsersApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<LoadingHttpMessageHandler>();

builder.Services.AddRefitClient<IStatsApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<LoadingHttpMessageHandler>();

builder.Services.AddRefitClient<IInvitationsApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
    .AddHttpMessageHandler<LoadingHttpMessageHandler>();

await builder.Build().RunAsync();
