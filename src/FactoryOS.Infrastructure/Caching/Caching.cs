using FactoryOS.Application.Caching;
using FactoryOS.Infrastructure.Configuration;
using FactoryOS.Infrastructure.Serialization;
using FactoryOS.Shared.Guards;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactoryOS.Infrastructure.Caching;

/// <summary>
/// The default <see cref="ICacheKeyGenerator"/>. Composes a prefix and segments into a colon-delimited key, mirroring
/// the tenant-namespaced convention the shared kernel's cache-key builders use.
/// </summary>
public sealed class CacheKeyGenerator : ICacheKeyGenerator
{
    private const char Separator = ':';

    /// <inheritdoc />
    public string Generate(string prefix, params ReadOnlySpan<string> segments)
    {
        Guard.AgainstNullOrWhiteSpace(prefix);

        if (segments.Length == 0)
        {
            return prefix;
        }

        var builder = new System.Text.StringBuilder(prefix);
        foreach (var segment in segments)
        {
            Guard.AgainstNullOrWhiteSpace(segment);
            builder.Append(Separator).Append(segment);
        }

        return builder.ToString();
    }
}

/// <summary>
/// The default <see cref="ICacheProvider"/>, backed by an in-process <see cref="IMemoryCache"/>. It is the local,
/// single-node store; a distributed provider (for example Redis) can replace it behind the same abstraction.
/// </summary>
public sealed class MemoryCacheProvider : ICacheProvider
{
    private readonly IMemoryCache _cache;

    /// <summary>Initializes a new instance of the <see cref="MemoryCacheProvider"/> class.</summary>
    /// <param name="cache">The backing in-memory cache.</param>
    public MemoryCacheProvider(IMemoryCache cache)
    {
        _cache = Guard.AgainstNull(cache);
    }

    /// <inheritdoc />
    public Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        Guard.AgainstNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        _cache.TryGetValue(key, out byte[]? value);
        return Task.FromResult(value);
    }

    /// <inheritdoc />
    public Task SetAsync(
        string key,
        byte[] value,
        TimeSpan? timeToLive = null,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstNullOrWhiteSpace(key);
        Guard.AgainstNull(value);
        cancellationToken.ThrowIfCancellationRequested();

        var options = new MemoryCacheEntryOptions();
        if (timeToLive is { } ttl)
        {
            options.AbsoluteExpirationRelativeToNow = ttl;
        }

        _cache.Set(key, value, options);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        Guard.AgainstNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        _cache.Remove(key);
        return Task.CompletedTask;
    }
}

/// <summary>
/// The default <see cref="ICacheService"/>: a typed facade over an <see cref="ICacheProvider"/> that serializes values
/// as JSON and applies the configured default time-to-live when a caller specifies none.
/// </summary>
public sealed class CacheService : ICacheService
{
    private readonly ICacheProvider _provider;
    private readonly IJsonSerializer _serializer;
    private readonly ILogger<CacheService> _logger;
    private readonly TimeSpan _defaultTimeToLive;

    /// <summary>Initializes a new instance of the <see cref="CacheService"/> class.</summary>
    /// <param name="provider">The low-level cache store.</param>
    /// <param name="serializer">The serializer used to marshal values to and from bytes.</param>
    /// <param name="options">The infrastructure options carrying the default time-to-live.</param>
    /// <param name="logger">The logger used to trace cache population.</param>
    public CacheService(
        ICacheProvider provider,
        IJsonSerializer serializer,
        IOptions<InfrastructureOptions> options,
        ILogger<CacheService> logger)
    {
        _provider = Guard.AgainstNull(provider);
        _serializer = Guard.AgainstNull(serializer);
        _logger = Guard.AgainstNull(logger);
        Guard.AgainstNull(options);
        _defaultTimeToLive = options.Value.DefaultCacheTimeToLive;
    }

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        Guard.AgainstNullOrWhiteSpace(key);

        var bytes = await _provider.GetAsync(key, cancellationToken);
        return bytes is null ? default : _serializer.Deserialize<T>(bytes);
    }

    /// <inheritdoc />
    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? timeToLive = null,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstNullOrWhiteSpace(key);

        var bytes = _serializer.SerializeToUtf8Bytes(value);
        return _provider.SetAsync(key, bytes, timeToLive ?? _defaultTimeToLive, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? timeToLive = null,
        CancellationToken cancellationToken = default)
    {
        Guard.AgainstNullOrWhiteSpace(key);
        Guard.AgainstNull(factory);

        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        _logger.LogDebug("Cache miss for key {CacheKey}; populating.", key);
        var created = await factory(cancellationToken);
        await SetAsync(key, created, timeToLive, cancellationToken);
        return created;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        Guard.AgainstNullOrWhiteSpace(key);
        return _provider.RemoveAsync(key, cancellationToken);
    }
}
