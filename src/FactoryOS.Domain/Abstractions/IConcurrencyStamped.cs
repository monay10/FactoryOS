namespace FactoryOS.Domain.Abstractions;

/// <summary>
/// Opt-in contract for entities guarded by optimistic concurrency. The persistence layer stamps a
/// fresh token on every write and includes the original token in the update predicate, so a stale
/// write is detected and rejected.
/// </summary>
public interface IConcurrencyStamped
{
    /// <summary>Gets the current concurrency token.</summary>
    Guid ConcurrencyToken { get; }

    /// <summary>Stamps a new concurrency token.</summary>
    /// <param name="token">The new token.</param>
    void StampConcurrency(Guid token);
}
