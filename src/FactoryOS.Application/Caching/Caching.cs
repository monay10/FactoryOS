namespace FactoryOS.Application.Caching;

/// <summary>Builds cache keys with a consistent, collision-resistant scheme.</summary>
public interface ICacheKeyGenerator
{
    /// <summary>Builds a cache key from a prefix and segments.</summary>
    /// <param name="prefix">The logical prefix (for example a tenant or entity name).</param>
    /// <param name="segments">The additional segments that make the key unique.</param>
    /// <returns>The composed cache key.</returns>
    string Generate(string prefix, params ReadOnlySpan<string> segments);
}

/// <summary>The low-level cache store: opaque bytes keyed by string, with an optional time-to-live.</summary>
public interface ICacheProvider
{
    /// <summary>Reads a raw cache entry.</summary>
    /// <param name="key">The cache key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The stored bytes, or <see langword="null"/> on a miss.</returns>
    Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Writes a raw cache entry.</summary>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The bytes to store.</param>
    /// <param name="timeToLive">An optional expiry; <see langword="null"/> for no expiry.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the entry has been written.</returns>
    Task SetAsync(string key, byte[] value, TimeSpan? timeToLive = null, CancellationToken cancellationToken = default);

    /// <summary>Removes a cache entry.</summary>
    /// <param name="key">The cache key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the entry has been removed.</returns>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>The high-level, typed cache facade built over an <see cref="ICacheProvider"/>.</summary>
public interface ICacheService
{
    /// <summary>Reads a typed cache entry.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The cached value, or <see langword="null"/> on a miss.</returns>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>Writes a typed cache entry.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="timeToLive">An optional expiry; <see langword="null"/> for no expiry.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the value has been cached.</returns>
    Task SetAsync<T>(string key, T value, TimeSpan? timeToLive = null, CancellationToken cancellationToken = default);

    /// <summary>Reads a cached value, creating and caching it on a miss.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="factory">The factory that produces the value on a miss.</param>
    /// <param name="timeToLive">An optional expiry; <see langword="null"/> for no expiry.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The cached or newly created value.</returns>
    Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? timeToLive = null,
        CancellationToken cancellationToken = default);

    /// <summary>Removes a cache entry.</summary>
    /// <param name="key">The cache key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the entry has been removed.</returns>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
