using System.Collections.Concurrent;
using FactoryOS.Connectors.Runtime.Domain;
using FactoryOS.Domain.Abstractions;

namespace FactoryOS.Connectors.Runtime.Execution;

/// <summary>A rate limit's state at one moment.</summary>
/// <param name="Key">The limit key.</param>
/// <param name="Used">How many permits have been taken in the current window.</param>
/// <param name="Permits">How many permits the window holds.</param>
/// <param name="WindowStartedUtc">When the current window began.</param>
public sealed record RateLimitSnapshot(string Key, int Used, int Permits, DateTimeOffset WindowStartedUtc)
{
    /// <summary>Gets how many permits remain in the current window.</summary>
    public int Remaining => Math.Max(0, Permits - Used);
}

/// <summary>
/// Keeps a connector instance inside the call quota its external system publishes.
/// <para>
/// A fixed window, not a leaky bucket, because that is how the quotas are actually written — "1000 calls per
/// minute" — and a limiter that does not match the quota it protects is one nobody can reason about when the
/// vendor sends the throttling notice.
/// </para>
/// <para>
/// The limit is per tenant and instance. One factory exhausting its ERP quota must not throttle another's.
/// </para>
/// </summary>
public sealed class RateLimiter
{
    private sealed record Window(DateTimeOffset StartedUtc, int Used);

    private readonly ConcurrentDictionary<string, Window> _windows = new(StringComparer.Ordinal);
    private readonly IDateTimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="RateLimiter"/> class.</summary>
    /// <param name="clock">The clock that rolls the windows.</param>
    public RateLimiter(IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    /// <summary>Builds the key one limit is tracked under.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="instance">The instance key.</param>
    /// <returns>The limit key.</returns>
    public static string KeyFor(string tenant, string instance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(instance);
        return $"{tenant}|{instance}";
    }

    /// <summary>Takes a permit if one is available.</summary>
    /// <param name="key">The limit key.</param>
    /// <param name="limit">The limit policy.</param>
    /// <returns><see langword="true"/> when a permit was taken and the call may proceed.</returns>
    public bool TryAcquire(string key, ConnectorRateLimit limit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(limit);

        if (!limit.Enabled)
        {
            return true;
        }

        var now = _clock.UtcNow;
        var acquired = false;

        _windows.AddOrUpdate(
            key,
            _ =>
            {
                acquired = true;
                return new Window(now, 1);
            },
            (_, current) =>
            {
                if (now - current.StartedUtc >= limit.Window)
                {
                    acquired = true;
                    return new Window(now, 1);
                }

                if (current.Used >= limit.Permits)
                {
                    acquired = false;
                    return current;
                }

                acquired = true;
                return current with { Used = current.Used + 1 };
            });

        return acquired;
    }

    /// <summary>Reads a limit's state without taking a permit.</summary>
    /// <param name="key">The limit key.</param>
    /// <param name="limit">The limit policy.</param>
    /// <returns>The snapshot; a limit never exercised reads as untouched.</returns>
    public RateLimitSnapshot Snapshot(string key, ConnectorRateLimit limit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(limit);

        var now = _clock.UtcNow;
        if (!_windows.TryGetValue(key, out var window) || now - window.StartedUtc >= limit.Window)
        {
            return new RateLimitSnapshot(key, 0, limit.Permits, now);
        }

        return new RateLimitSnapshot(key, window.Used, limit.Permits, window.StartedUtc);
    }

    /// <summary>Clears a limit's window, for an operator whose quota was raised.</summary>
    /// <param name="key">The limit key.</param>
    public void Reset(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _windows.TryRemove(key, out _);
    }
}
