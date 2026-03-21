using System.Security.Claims;

namespace ZISK.Services;

public interface IAuditService
{
    void Log(string action, string entity, string entityId, ClaimsPrincipal user, object? details = null);
}
