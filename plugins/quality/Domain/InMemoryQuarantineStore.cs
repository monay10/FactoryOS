using System.Collections.Concurrent;

namespace FactoryOS.Plugins.Quality.Domain;

/// <summary>
/// The default in-memory <see cref="IQuarantineStore"/>: a per-tenant set of quarantined line ids. Each tenant has
/// its own bucket, so no tenant can read or affect another's. Replaceable by an EF Core-backed store behind the
/// interface.
/// </summary>
public sealed class InMemoryQuarantineStore : IQuarantineStore
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _byTenant =
        new(StringComparer.Ordinal);

    /// <inheritdoc />
    public bool TryQuarantine(string tenant, string lineId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(lineId);

        var bucket = _byTenant.GetOrAdd(tenant, static _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));
        return bucket.TryAdd(lineId, 0);
    }

    /// <inheritdoc />
    public bool IsQuarantined(string tenant, string lineId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(lineId);

        return _byTenant.TryGetValue(tenant, out var bucket) && bucket.ContainsKey(lineId);
    }
}
