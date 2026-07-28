namespace ZISK.Services;

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
