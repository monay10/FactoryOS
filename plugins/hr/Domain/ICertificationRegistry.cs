namespace FactoryOS.Plugins.Hr.Domain;

/// <summary>
/// Records each worker's certifications and their expiries, per tenant. Recording is last-write-wins, so a
/// redelivered record is harmless. Tenant-scoped through the key.
/// </summary>
public interface ICertificationRegistry
{
    /// <summary>Records (or replaces) the expiry of a worker's certification.</summary>
    /// <param name="key">The worker.</param>
    /// <param name="certification">The certification code.</param>
    /// <param name="expiresAt">When it expires.</param>
    void Record(WorkerKey key, string certification, DateTimeOffset expiresAt);

    /// <summary>Returns the expiry of a worker's certification, or <see langword="null"/> if not held.</summary>
    /// <param name="key">The worker.</param>
    /// <param name="certification">The certification code.</param>
    /// <returns>The expiry, or <see langword="null"/>.</returns>
    DateTimeOffset? ExpiryOf(WorkerKey key, string certification);
}
