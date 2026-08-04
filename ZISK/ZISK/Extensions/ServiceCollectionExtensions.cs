using Refit;
using ZISK.Client.Services;
using ZISK.Services;

namespace ZISK.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ICurrentLanguage, CurrentLanguage>();
        // MainLayout injects the client's ILanguageService and is static-SSR rendered on the
        // server before WASM boots - see ServerLanguageService for why it needs its own impl.
        services.AddScoped<ZISK.Client.Services.ILanguageService, ServerLanguageService>();
        services.AddScoped<IAttendanceService, AttendanceService>();
        services.AddScoped<IStatsService, StatsService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IExcuseService, ExcuseService>();
        services.AddScoped<ITrainingService, TrainingService>();
        services.AddScoped<IAnnouncementService, AnnouncementService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<ISeasonService, SeasonService>();
        services.AddScoped<ITrainingSeriesService, TrainingSeriesService>();
        services.AddScoped<IParentInvitationService, ParentInvitationService>();
        // These workers are registered as Scoped, not as hosted services, because they depend on DbContext.
        // They are triggered manually from a controller or scheduler, not by the DI host automatically.
        services.AddScoped<ChildUpgradeWorker>();
        services.AddScoped<TrainingSeriesGeneratorWorker>();
        services.AddScoped<AttendanceAutoCloseWorker>();
        return services;
    }

    public static IServiceCollection AddRefitClients(this IServiceCollection services, IConfiguration configuration)
    {
        // Mostly a placeholder: ForwardAuthHeaderHandler rewrites the host of every one of these
        // calls to whatever host the current request arrived on, because they are self-calls back
        // into this same app. This value only survives for calls made with no HttpContext.
        var baseAddressString = configuration["ApiBaseAddress"] ?? "http://localhost:5224";
        var baseAddress = new Uri(baseAddressString);

        // No explicit RefitSettings here on purpose: Refit's default content serializer already
        // reads camelCase and string-valued enums, which is what the API emits, so these clients
        // match ZISK.Client/Program.cs behaviourally even though that one spells the options out.
        //
        // Every Refit client gets ForwardAuthHeaderHandler so that Blazor SSR requests carry the auth cookie.
        // See ForwardAuthHeaderHandler for a full explanation of why this is needed.
        services.AddRefitClient<IExcusesApi>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
            .AddHttpMessageHandler<ForwardAuthHeaderHandler>();
        services.AddRefitClient<IAttendanceApi>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
            .AddHttpMessageHandler<ForwardAuthHeaderHandler>();
        services.AddRefitClient<ITrainingsApi>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
            .AddHttpMessageHandler<ForwardAuthHeaderHandler>();
        services.AddRefitClient<ITeamsApi>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
            .AddHttpMessageHandler<ForwardAuthHeaderHandler>();
        services.AddRefitClient<IAnnouncementsApi>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
            .AddHttpMessageHandler<ForwardAuthHeaderHandler>();
        services.AddRefitClient<IDocumentsApi>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
            .AddHttpMessageHandler<ForwardAuthHeaderHandler>();
        services.AddRefitClient<IChildrenApi>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
            .AddHttpMessageHandler<ForwardAuthHeaderHandler>();
        services.AddRefitClient<IUsersApi>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
            .AddHttpMessageHandler<ForwardAuthHeaderHandler>();
        services.AddRefitClient<IMeApi>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
            .AddHttpMessageHandler<ForwardAuthHeaderHandler>();
        services.AddRefitClient<IStatsApi>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
            .AddHttpMessageHandler<ForwardAuthHeaderHandler>();
        services.AddRefitClient<ISeasonsApi>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
            .AddHttpMessageHandler<ForwardAuthHeaderHandler>();
        services.AddRefitClient<ITrainingSeriesApi>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
            .AddHttpMessageHandler<ForwardAuthHeaderHandler>();
        services.AddRefitClient<IInvitationsApi>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
            .AddHttpMessageHandler<ForwardAuthHeaderHandler>();
        services.AddRefitClient<IDemoApi>()
            .ConfigureHttpClient(c => c.BaseAddress = baseAddress)
            .AddHttpMessageHandler<ForwardAuthHeaderHandler>();

        return services;
    }
}
