using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using Refit;
using ZISK.Client.Services;
using ZISK.Components;
using ZISK.Components.Account;
using ZISK.Data;
using ZISK.Data.Entities;

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

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();
builder.Services.AddMudServices();

var baseAddress = new Uri("http://localhost:5224");
builder.Services.AddRefitClient<IExcusesApi>()
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress);
builder.Services.AddRefitClient<IAttendanceApi>()
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress);
builder.Services.AddRefitClient<ITrainingsApi>()
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress);
builder.Services.AddRefitClient<ITeamsApi>()
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress);
builder.Services.AddRefitClient<IAnnouncementsApi>()
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress);
builder.Services.AddRefitClient<IDocumentsApi>()
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress);
builder.Services.AddRefitClient<IChildrenApi>()
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress);
builder.Services.AddRefitClient<IUsersApi>()
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress);
builder.Services.AddRefitClient<IStatsApi>()
    .ConfigureHttpClient(c => c.BaseAddress = baseAddress);

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

// AI generovanie sample dat
// Seed data
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    // Seed roles
    string[] roles = ["Admin", "Coach", "Parent"];
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    // Seed admin user
    var adminEmail = "admin@zisk.sk";
    ApplicationUser? admin = null;
    if (await userManager.FindByEmailAsync(adminEmail) == null)
    {
        admin = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FirstName = "Admin",
            LastName = "ZISK",
            EmailConfirmed = true,
            IsActive = true
        };

        var result = await userManager.CreateAsync(admin, "admin123");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, "Admin");
        }
    }

    // Seed coach user
    var coachEmail = "trener@zisk.sk";
    if (await userManager.FindByEmailAsync(coachEmail) == null)
    {
        var coach = new ApplicationUser
        {
            UserName = coachEmail,
            Email = coachEmail,
            FirstName = "Ján",
            LastName = "Tréner",
            EmailConfirmed = true,
            IsActive = true
        };

        var result = await userManager.CreateAsync(coach, "trener123");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(coach, "Coach");
        }
    }

    // Seed parent user
    var parentEmail = "rodic@zisk.sk";
    ApplicationUser? parent = null;
    if (await userManager.FindByEmailAsync(parentEmail) == null)
    {
        parent = new ApplicationUser
        {
            UserName = parentEmail,
            Email = parentEmail,
            FirstName = "Peter",
            LastName = "Rodič",
            EmailConfirmed = true,
            IsActive = true
        };

        var result = await userManager.CreateAsync(parent, "rodic123");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(parent, "Parent");
        }
    }
    else
    {
        parent = await userManager.FindByEmailAsync(parentEmail);
    }

    // Seed teams
    if (!await context.Teams.AnyAsync())
    {
        var teams = new List<Team>
        {
            new() { Id = Guid.NewGuid(), Name = "A-tím", ShortName = "A", Description = "Hlavný seniorský tím", IsActive = true },
            new() { Id = Guid.NewGuid(), Name = "B-tím", ShortName = "B", Description = "Záložný seniorský tím", IsActive = true },
            new() { Id = Guid.NewGuid(), Name = "Žiaci", ShortName = "Ž", Description = "Mládežnícky tím", IsActive = true },
            new() { Id = Guid.NewGuid(), Name = "Prípravka", ShortName = "P", Description = "Najmenší športovci", IsActive = true }
        };

        context.Teams.AddRange(teams);
        await context.SaveChangesAsync();
    }

    // Seed children
    if (!await context.ChildProfiles.AnyAsync())
    {
        var aTeam = await context.Teams.FirstOrDefaultAsync(t => t.Name == "A-tím");
        var bTeam = await context.Teams.FirstOrDefaultAsync(t => t.Name == "B-tím");
        var youth = await context.Teams.FirstOrDefaultAsync(t => t.Name == "Žiaci");

        var children = new List<ChildProfile>
        {
            new() { Id = Guid.NewGuid(), FirstName = "Ján", LastName = "Novák", DateOfBirth = new DateOnly(2010, 5, 15), TeamId = aTeam?.Id, IsActive = true },
            new() { Id = Guid.NewGuid(), FirstName = "Peter", LastName = "Horváth", DateOfBirth = new DateOnly(2011, 3, 22), TeamId = aTeam?.Id, IsActive = true },
            new() { Id = Guid.NewGuid(), FirstName = "Mária", LastName = "Kováčová", DateOfBirth = new DateOnly(2012, 7, 8), TeamId = bTeam?.Id, IsActive = true },
            new() { Id = Guid.NewGuid(), FirstName = "Lucia", LastName = "Varga", DateOfBirth = new DateOnly(2013, 11, 30), TeamId = youth?.Id, IsActive = true },
            new() { Id = Guid.NewGuid(), FirstName = "Martin", LastName = "Balog", DateOfBirth = new DateOnly(2010, 1, 5), TeamId = aTeam?.Id, IsActive = true }
        };

        context.ChildProfiles.AddRange(children);
        await context.SaveChangesAsync();

        if (parent != null)
        {
            context.ParentChildren.Add(new ParentChild
            {
                ParentId = parent.Id,
                ChildId = children.First().Id,
                IsPrimary = true
            });
            await context.SaveChangesAsync();
        }
    }
}

app.Run();