using Microsoft.Extensions.Configuration;

namespace ZISK.Services;

public enum SeedMode { Local, Demo, Production }

/// <summary>
/// Shared with Program.cs (to pick the email sender) as well as DatabaseInitializer
/// (to pick the seed data path) so the ZISK_SEED_MODE parsing rule lives in one place.
/// </summary>
public static class SeedModeResolver
{
    public static SeedMode Resolve(IConfiguration configuration)
    {
        var raw = configuration["ZISK_SEED_MODE"];
        if (string.IsNullOrWhiteSpace(raw))
            return SeedMode.Local;

        return raw.Trim().ToLowerInvariant() switch
        {
            "local" => SeedMode.Local,
            "demo" => SeedMode.Demo,
            "production" => SeedMode.Production,
            _ => throw new SeedConfigurationException(
                $"Neznáma hodnota ZISK_SEED_MODE='{raw}'. Platné hodnoty sú: local, demo, production.")
        };
    }
}

public class SeedPasswordOptions
{
    public string? Admin { get; set; }
    public string? Coach { get; set; }
    public string? Parent { get; set; }
    public string? Child { get; set; }
}

public class SeedInitialAdminOptions
{
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string FirstName { get; set; } = "Admin";
    public string LastName { get; set; } = "ZISK";
}

/// <summary>
/// Thrown when ZISK_SEED_MODE=demo/production is missing required Seed:* configuration.
/// Deliberately not swallowed by the generic startup catch in Program.cs — a misconfigured
/// demo/production deploy must fail loudly, not silently fall back to hardcoded local passwords.
/// </summary>
public sealed class SeedConfigurationException(string message) : InvalidOperationException(message);
