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

var baseAddress = new Uri(builder.HostEnvironment.BaseAddress);

var refitSettings = new RefitSettings
{
    ContentSerializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    })
};

builder.Services.AddRefitClient<IExcusesApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress);

builder.Services.AddRefitClient<IAttendanceApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress);

builder.Services.AddRefitClient<ITrainingsApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress);

builder.Services.AddRefitClient<ITeamsApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress);

builder.Services.AddRefitClient<IAnnouncementsApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress);

builder.Services.AddRefitClient<IDocumentsApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress);

builder.Services.AddRefitClient<IChildrenApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress);

builder.Services.AddRefitClient<IUsersApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress);

builder.Services.AddRefitClient<IStatsApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress);

await builder.Build().RunAsync();
