using FactoryOS.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FactoryOS.Persistence.Auditing;

/// <summary>
/// A <see cref="SaveChangesInterceptor"/> that enforces the platform's cross-cutting persistence
/// rules on every write: it fills audit metadata, converts hard deletes of soft-deletable entities
/// into flag updates, and stamps a fresh optimistic-concurrency token.
/// </summary>
public sealed class AuditingSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IDateTimeProvider _clock;
    private readonly ICurrentActorProvider _actor;

    /// <summary>Initializes a new instance of the <see cref="AuditingSaveChangesInterceptor"/> class.</summary>
    /// <param name="clock">The clock used for audit timestamps.</param>
    /// <param name="actor">The current-actor provider.</param>
    public AuditingSaveChangesInterceptor(IDateTimeProvider clock, ICurrentActorProvider actor)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(actor);

        _clock = clock;
        _actor = actor;
    }

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = _clock.UtcNow;
        var actor = _actor.ActorId;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    (entry.Entity as IAuditableEntity)?.ApplyCreated(now, actor);
                    Restamp(entry);
                    break;

                case EntityState.Modified:
                    (entry.Entity as IAuditableEntity)?.ApplyModified(now, actor);
                    Restamp(entry);
                    break;

                case EntityState.Deleted when entry.Entity is ISoftDeletable soft:
                    entry.State = EntityState.Modified;
                    soft.ApplyDeleted(now, actor);
                    (entry.Entity as IAuditableEntity)?.ApplyModified(now, actor);
                    Restamp(entry);
                    break;

                default:
                    break;
            }
        }
    }

    private static void Restamp(EntityEntry entry)
    {
        if (entry.Entity is IConcurrencyStamped stamped)
        {
            stamped.StampConcurrency(Guid.CreateVersion7());
        }
    }
}
