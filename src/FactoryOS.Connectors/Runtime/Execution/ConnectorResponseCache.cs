using System.Collections.Concurrent;
using FactoryOS.Connectors.Runtime.Domain;
using FactoryOS.Domain.Abstractions;

namespace FactoryOS.Connectors.Runtime.Execution;

/// <summary>
/// Reuses the responses of read operations that were asked for again inside their freshness window.
/// <para>
/// Three rules hold and none of them is configurable. Only operations a definition marks <b>cacheable</b>
/// are cached — and the compatibility validator refuses a cacheable operation that is not idempotent, so a
/// side effect can never be hidden behind a cache hit. Only <b>successful</b> responses are cached; caching a
/// failure would turn one bad moment into a minute of them. And the key is <b>tenant-qualified</b>, so no
/// factory can ever be served another factory's data out of a shared cache.
/// </para>
/// </summary>
public sealed class ConnectorResponseCache
{
    private sealed record Entry(ConnectorResponse Response, DateTimeOffset StoredUtc, DateTimeOffset ExpiresUtc);

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly IDateTimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="ConnectorResponseCache"/> class.</summary>
    /// <param name="clock">The clock that decides freshness.</param>
    public ConnectorResponseCache(IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    /// <summary>Gets how many responses are held.</summary>
    public int Count => _entries.Count;

    /// <summary>Looks for a fresh response to a request.</summary>
    /// <param name="request">The request.</param>
    /// <param name="policy">The cache policy.</param>
    /// <param name="operation">The operation being invoked.</param>
    /// <returns>The cached response marked as such, or <see langword="null"/> when there is no fresh one.</returns>
    public ConnectorResponse? Find(
        ConnectorRequest request, ConnectorCachePolicy policy, ConnectorOperation operation)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(operation);

        if (!policy.Enabled || !operation.Cacheable)
        {
            return null;
        }

        var key = request.CacheKey();
        if (!_entries.TryGetValue(key, out var entry))
        {
            return null;
        }

        if (_clock.UtcNow >= entry.ExpiresUtc)
        {
            _entries.TryRemove(key, out _);
            return null;
        }

        return entry.Response with { FromCache = true, Correlation = request.Correlation };
    }

    /// <summary>Stores a response, if it is one that may be reused.</summary>
    /// <param name="request">The request it answers.</param>
    /// <param name="response">The response.</param>
    /// <param name="policy">The cache policy.</param>
    /// <param name="operation">The operation that was invoked.</param>
    public void Store(
        ConnectorRequest request,
        ConnectorResponse response,
        ConnectorCachePolicy policy,
        ConnectorOperation operation)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(operation);

        if (!policy.Enabled || !operation.Cacheable || !response.Succeeded || response.FromCache)
        {
            return;
        }

        var now = _clock.UtcNow;
        _entries[request.CacheKey()] = new Entry(
            response with { FromCache = false },
            now,
            now + policy.TimeToLive);

        Trim(policy.Capacity);
    }

    /// <summary>Forgets everything cached for one tenant — what an operator does after a bad import.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>How many entries were forgotten.</returns>
    public int InvalidateTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        var prefix = tenant + "|";
        var removed = 0;
        foreach (var key in _entries.Keys.Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            if (_entries.TryRemove(key, out _))
            {
                removed++;
            }
        }

        return removed;
    }

    /// <summary>Forgets everything cached for one tenant's instance.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="instance">The instance key.</param>
    /// <returns>How many entries were forgotten.</returns>
    public int InvalidateInstance(string tenant, string instance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(instance);

        var prefix = $"{tenant}|{instance}|";
        var removed = 0;
        foreach (var key in _entries.Keys.Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            if (_entries.TryRemove(key, out _))
            {
                removed++;
            }
        }

        return removed;
    }

    private void Trim(int capacity)
    {
        if (_entries.Count <= capacity)
        {
            return;
        }

        foreach (var key in _entries
                     .OrderBy(pair => pair.Value.StoredUtc)
                     .Take(_entries.Count - capacity)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _entries.TryRemove(key, out _);
        }
    }
}
