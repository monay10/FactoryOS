using System.Collections.Concurrent;
using FactoryOS.Domain.Abstractions;
using FactoryOS.Identity.Authorization.Configuration;
using Microsoft.Extensions.Options;

namespace FactoryOS.Identity.Authorization.Caching;

/// <summary>
/// A small time-bounded cache shared by the permission, role and policy services. When caching is disabled
/// (<see cref="PermissionCacheOptions.Enabled"/> is <see langword="false"/>) it always invokes the factory.
/// </summary>
public interface IAuthorizationCache
{
    /// <summary>Returns the cached value for a key, computing and storing it on a miss or expiry.</summary>
    /// <typeparam name="T">The cached value type.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="factory">The factory that computes the value on a miss.</param>
    /// <returns>The cached or freshly computed value.</returns>
    T GetOrAdd<T>(string key, Func<T> factory);

    /// <summary>Invalidates a single key.</summary>
    /// <param name="key">The cache key to remove.</param>
    void Invalidate(string key);

    /// <summary>Clears every cached entry.</summary>
    void Clear();
}

/// <summary>An in-memory <see cref="IAuthorizationCache"/> keyed by string, with a per-entry expiry.</summary>
public sealed class InMemoryAuthorizationCache : IAuthorizationCache
{
    private readonly ConcurrentDictionary<string, (DateTimeOffset ExpiresOnUtc, object? Value)> _entries =
        new(StringComparer.Ordinal);

    private readonly IDateTimeProvider _clock;
    private readonly PermissionCacheOptions _options;

    /// <summary>Initializes a new instance of the <see cref="InMemoryAuthorizationCache"/> class.</summary>
    /// <param name="clock">The clock used for entry expiry.</param>
    /// <param name="options">The authorization options carrying the cache policy.</param>
    public InMemoryAuthorizationCache(IDateTimeProvider clock, IOptions<AuthorizationOptions> options)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(options);

        _clock = clock;
        _options = options.Value.PermissionCache;
    }

    /// <inheritdoc />
    public T GetOrAdd<T>(string key, Func<T> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        if (!_options.Enabled)
        {
            return factory();
        }

        var now = _clock.UtcNow;
        if (_entries.TryGetValue(key, out var entry) && now < entry.ExpiresOnUtc)
        {
            return (T)entry.Value!;
        }

        var value = factory();
        _entries[key] = (now.AddSeconds(_options.TtlSeconds), value);
        return value;
    }

    /// <inheritdoc />
    public void Invalidate(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _entries.TryRemove(key, out _);
    }

    /// <inheritdoc />
    public void Clear() => _entries.Clear();
}
