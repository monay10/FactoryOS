using System.Collections.Concurrent;

namespace FactoryOS.Plugins.Hr.Domain;

/// <summary>
/// The default in-memory <see cref="ICertificationRegistry"/>: a per-worker map of certification code to expiry.
/// Thread-safe. Replaceable by an EF Core-backed store behind the interface.
/// </summary>
public sealed class InMemoryCertificationRegistry : ICertificationRegistry
{
    private readonly ConcurrentDictionary<WorkerKey, ConcurrentDictionary<string, DateTimeOffset>> _byWorker = new();

    /// <inheritdoc />
    public void Record(WorkerKey key, string certification, DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(certification);
        var certs = _byWorker.GetOrAdd(key, static _ => new ConcurrentDictionary<string, DateTimeOffset>(StringComparer.Ordinal));
        certs[certification] = expiresAt;
    }

    /// <inheritdoc />
    public DateTimeOffset? ExpiryOf(WorkerKey key, string certification)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(certification);
        return _byWorker.TryGetValue(key, out var certs) && certs.TryGetValue(certification, out var expiry)
            ? expiry
            : null;
    }
}
