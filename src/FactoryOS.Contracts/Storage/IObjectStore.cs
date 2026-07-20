namespace FactoryOS.Contracts.Storage;

/// <summary>
/// The Platform-layer object store — the Standard Model's door to blob storage, abstracting MinIO/S3 the way the
/// event bus abstracts the broker. Every operation takes the tenant explicitly; there is no code path that reads
/// or writes across tenants. The in-memory implementation is the default; a MinIO/S3-backed store replaces it
/// behind this interface without touching callers.
/// </summary>
public interface IObjectStore
{
    /// <summary>Stores an object, replacing any existing object with the same tenant and key.</summary>
    /// <param name="stored">The object to store; its tenant scopes the write.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task PutAsync(StoredObject stored, CancellationToken cancellationToken);

    /// <summary>Gets an object by key, or <see langword="null"/> if the tenant has no such object.</summary>
    /// <param name="tenant">The owning tenant.</param>
    /// <param name="key">The object key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The stored object, or <see langword="null"/> when absent.</returns>
    Task<StoredObject?> GetAsync(string tenant, string key, CancellationToken cancellationToken);

    /// <summary>Reports whether an object exists for a tenant.</summary>
    /// <param name="tenant">The owning tenant.</param>
    /// <param name="key">The object key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> if the object exists.</returns>
    Task<bool> ExistsAsync(string tenant, string key, CancellationToken cancellationToken);

    /// <summary>Lists a tenant's objects whose key starts with <paramref name="prefix"/>, ordered by key.</summary>
    /// <param name="tenant">The tenant to list within.</param>
    /// <param name="prefix">The key prefix to filter by; an empty prefix lists all of the tenant's objects.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The matching object references, ordered by key.</returns>
    Task<IReadOnlyList<ObjectRef>> ListAsync(string tenant, string prefix, CancellationToken cancellationToken);
}
