using System.Security.Claims;
using System.Text.Json;

namespace ZISK.Services;

public class AuditService : IAuditService
{
    private readonly ILogger<AuditService> _logger;

    public AuditService(ILogger<AuditService> logger)
    {
        _logger = logger;
    }

    public void Log(string action, string entity, string entityId, ClaimsPrincipal? user, object? details = null)
    {
        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        var role = user?.FindFirstValue(ClaimTypes.Role) ?? "unknown";
        var payload = details is null ? string.Empty : JsonSerializer.Serialize(details);

        _logger.LogInformation(
            "AUDIT | Action: {Action} | Entity: {Entity} | EntityId: {EntityId} | UserId: {UserId} | Role: {Role} | Details: {Details}",
            action,
            entity,
            entityId,
            userId,
            role,
            payload);
    }
}
