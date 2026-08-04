using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ZISK.Data.Entities;

namespace ZISK.Services.Demo;

/// <summary>
/// Stamps newly-added <see cref="IDemoScoped"/> entities with the current demo session id.
/// Exists so service/controller code never has to know demo sessions exist: e.g. an admin
/// creating a user while browsing inside their cloned session gets a user that stays inside
/// that clone automatically, via the exact same DemoSessionId the query filter in
/// ApplicationDbContext already scopes reads by.
///
/// Only fills gaps - it never overrides an entity that already has an explicit
/// DemoSessionId. That matters for DemoSessionService, which clones template rows into a
/// brand new session by setting DemoSessionId explicitly on each copy before adding it; those
/// must not be silently re-stamped with whatever the ambient session happens to be.
/// </summary>
public class DemoStampingInterceptor : SaveChangesInterceptor
{
    private readonly IDemoSessionContext _demoSessionContext;

    public DemoStampingInterceptor(IDemoSessionContext demoSessionContext)
    {
        _demoSessionContext = demoSessionContext;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        var current = _demoSessionContext.Current;
        if (context is null || current is null)
            return;

        foreach (var entry in context.ChangeTracker.Entries<IDemoScoped>())
        {
            if (entry.State == EntityState.Added && entry.Entity.DemoSessionId is null)
                entry.Entity.DemoSessionId = current;
        }
    }
}
