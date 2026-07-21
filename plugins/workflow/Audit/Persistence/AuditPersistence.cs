using System.Collections.Concurrent;
using FactoryOS.Plugins.Workflow.Audit.Domain;

namespace FactoryOS.Plugins.Workflow.Audit.Persistence;

/// <summary>The registry of retention and archive policies the audit engine applies.</summary>
public interface IAuditRepository
{
    /// <summary>Registers a retention policy.</summary>
    /// <param name="policy">The policy.</param>
    void RegisterRetention(AuditRetentionPolicy policy);

    /// <summary>Registers an archive policy.</summary>
    /// <param name="policy">The policy.</param>
    void RegisterArchive(AuditArchivePolicy policy);

    /// <summary>Gets the registered retention policies.</summary>
    /// <returns>The policies.</returns>
    IReadOnlyList<AuditRetentionPolicy> RetentionPolicies();

    /// <summary>Gets the registered archive policies.</summary>
    /// <returns>The policies.</returns>
    IReadOnlyList<AuditArchivePolicy> ArchivePolicies();
}

/// <summary>An in-memory <see cref="IAuditRepository"/>.</summary>
public sealed class InMemoryAuditRepository : IAuditRepository
{
    private readonly List<AuditRetentionPolicy> _retention = [];
    private readonly List<AuditArchivePolicy> _archive = [];
    private readonly Lock _gate = new();

    /// <inheritdoc />
    public void RegisterRetention(AuditRetentionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        lock (_gate)
        {
            _retention.Add(policy);
        }
    }

    /// <inheritdoc />
    public void RegisterArchive(AuditArchivePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        lock (_gate)
        {
            _archive.Add(policy);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<AuditRetentionPolicy> RetentionPolicies()
    {
        lock (_gate)
        {
            return _retention.ToArray();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<AuditArchivePolicy> ArchivePolicies()
    {
        lock (_gate)
        {
            return _archive.ToArray();
        }
    }
}

/// <summary>
/// The append-only store of audit records — the hot trail. It exposes no update operation at all: records go
/// in, are read back, and can only leave by being archived or expiring. That absence is deliberate and is half
/// of what "immutable" means here; the hash chain is the other half.
/// </summary>
public interface IAuditStore
{
    /// <summary>Appends a sealed record.</summary>
    /// <param name="record">The record.</param>
    void Append(AuditRecord record);

    /// <summary>Gets the last record in a tenant's chain, which the next record links to.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The head record, or <see langword="null"/> when the tenant has no records yet.</returns>
    AuditRecord? Head(string tenant);

    /// <summary>Gets a record by id.</summary>
    /// <param name="id">The record id.</param>
    /// <returns>The record, or <see langword="null"/> when not found.</returns>
    AuditRecord? Get(Guid id);

    /// <summary>Lists a tenant's records in chain order.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The records, ordered by sequence.</returns>
    IReadOnlyList<AuditRecord> ListByTenant(string tenant);

    /// <summary>Lists every record in the store, grouped by tenant and ordered by sequence.</summary>
    /// <returns>The records.</returns>
    IReadOnlyList<AuditRecord> All();

    /// <summary>Removes records from the hot store, having archived or expired them.</summary>
    /// <param name="ids">The record ids to remove.</param>
    /// <returns>How many records were removed.</returns>
    int Remove(IEnumerable<Guid> ids);
}

/// <summary>An in-memory, append-only <see cref="IAuditStore"/>.</summary>
public sealed class InMemoryAuditStore : IAuditStore
{
    private readonly ConcurrentDictionary<Guid, AuditRecord> _records = new();

    /// <inheritdoc />
    public void Append(AuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _records[record.Id] = record;
    }

    /// <inheritdoc />
    public AuditRecord? Head(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return _records.Values
            .Where(record => string.Equals(record.Tenant, tenant, StringComparison.Ordinal))
            .OrderByDescending(record => record.Sequence)
            .FirstOrDefault();
    }

    /// <inheritdoc />
    public AuditRecord? Get(Guid id) => _records.TryGetValue(id, out var record) ? record : null;

    /// <inheritdoc />
    public IReadOnlyList<AuditRecord> ListByTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return _records.Values
            .Where(record => string.Equals(record.Tenant, tenant, StringComparison.Ordinal))
            .OrderBy(record => record.Sequence)
            .ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<AuditRecord> All() => _records.Values
        .OrderBy(record => record.Tenant, StringComparer.Ordinal)
        .ThenBy(record => record.Sequence)
        .ToArray();

    /// <inheritdoc />
    public int Remove(IEnumerable<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return ids.Count(id => _records.TryRemove(id, out _));
    }
}

/// <summary>
/// The cold archive of audit records. Archived records keep their sequence numbers and hashes, so an archived
/// stretch of the chain can still be verified on its own and can be restored into the hot store unchanged.
/// </summary>
public interface IAuditArchiveRepository
{
    /// <summary>Archives a batch of records.</summary>
    /// <param name="records">The records to archive.</param>
    void Archive(IEnumerable<AuditRecord> records);

    /// <summary>Lists a tenant's archived records in chain order.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The archived records.</returns>
    IReadOnlyList<AuditRecord> ListByTenant(string tenant);

    /// <summary>Removes archived records that have outlived their retention.</summary>
    /// <param name="ids">The record ids to remove.</param>
    /// <returns>How many records were removed.</returns>
    int Remove(IEnumerable<Guid> ids);
}

/// <summary>An in-memory <see cref="IAuditArchiveRepository"/>.</summary>
public sealed class InMemoryAuditArchiveRepository : IAuditArchiveRepository
{
    private readonly ConcurrentDictionary<Guid, AuditRecord> _records = new();

    /// <inheritdoc />
    public void Archive(IEnumerable<AuditRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        foreach (var record in records)
        {
            _records[record.Id] = record;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<AuditRecord> ListByTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return _records.Values
            .Where(record => string.Equals(record.Tenant, tenant, StringComparison.Ordinal))
            .OrderBy(record => record.Sequence)
            .ToArray();
    }

    /// <inheritdoc />
    public int Remove(IEnumerable<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return ids.Count(id => _records.TryRemove(id, out _));
    }
}
