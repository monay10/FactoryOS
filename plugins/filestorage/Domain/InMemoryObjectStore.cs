using System.Collections.Concurrent;
using FactoryOS.Contracts.Storage;

namespace FactoryOS.Plugins.FileStorage.Domain;

/// <summary>
/// The default in-memory <see cref="IObjectStore"/>. Objects are partitioned by tenant — no code path crosses
/// tenants — and each tenant's bucket is guarded by its own lock. A put replaces any object with the same key.
/// This is the dev/test door; a MinIO/S3-backed store swaps in behind <see cref="IObjectStore"/> unchanged.
/// </summary>
public sealed class InMemoryObjectStore : IObjectStore
{
    private sealed class TenantBucket
    {
        public Lock Gate { get; } = new();

        public Dictionary<string, StoredObject> Objects { get; } = new(StringComparer.Ordinal);
    }

    private readonly ConcurrentDictionary<string, TenantBucket> _buckets = new(StringComparer.Ordinal);
    private readonly long _maxObjectSizeBytes;

    /// <summary>Initializes a new instance of the <see cref="InMemoryObjectStore"/> class.</summary>
    /// <param name="options">The module options carrying the object-size cap.</param>
    public InMemoryObjectStore(FileStorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _maxObjectSizeBytes = options.MaxObjectSizeBytes;
    }

    /// <inheritdoc />
    public Task PutAsync(StoredObject stored, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentException.ThrowIfNullOrWhiteSpace(stored.Tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(stored.Key);

        if (_maxObjectSizeBytes > 0 && stored.Size > _maxObjectSizeBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stored),
                stored.Size,
                $"Object '{stored.Key}' ({stored.Size} bytes) exceeds the configured limit of {_maxObjectSizeBytes} bytes.");
        }

        var bucket = _buckets.GetOrAdd(stored.Tenant, static _ => new TenantBucket());
        lock (bucket.Gate)
        {
            bucket.Objects[stored.Key] = stored;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<StoredObject?> GetAsync(string tenant, string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!_buckets.TryGetValue(tenant, out var bucket))
        {
            return Task.FromResult<StoredObject?>(null);
        }

        lock (bucket.Gate)
        {
            return Task.FromResult(bucket.Objects.GetValueOrDefault(key));
        }
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string tenant, string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!_buckets.TryGetValue(tenant, out var bucket))
        {
            return Task.FromResult(false);
        }

        lock (bucket.Gate)
        {
            return Task.FromResult(bucket.Objects.ContainsKey(key));
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ObjectRef>> ListAsync(string tenant, string prefix, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentNullException.ThrowIfNull(prefix);

        if (!_buckets.TryGetValue(tenant, out var bucket))
        {
            return Task.FromResult<IReadOnlyList<ObjectRef>>([]);
        }

        lock (bucket.Gate)
        {
            var refs = bucket.Objects.Values
                .Where(o => o.Key.StartsWith(prefix, StringComparison.Ordinal))
                .OrderBy(o => o.Key, StringComparer.Ordinal)
                .Select(o => new ObjectRef { Key = o.Key, ContentType = o.ContentType, Size = o.Size })
                .ToArray();

            return Task.FromResult<IReadOnlyList<ObjectRef>>(refs);
        }
    }
}
