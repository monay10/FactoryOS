using FactoryOS.Plugins.Workflow.Audit.Configuration;
using FactoryOS.Plugins.Workflow.Audit.Domain;
using FactoryOS.Plugins.Workflow.Audit.Persistence;

namespace FactoryOS.Plugins.Workflow.Audit.Execution;

/// <summary>
/// Moves records out of the hot store into the archive once an archive policy says their time there is up, and
/// brings them back on request. Archiving never alters a record: sequence numbers and hashes travel with it, so
/// an archived stretch verifies on its own and a restored record slots back into the chain unchanged.
/// </summary>
public sealed class AuditArchiveManager
{
    private readonly IAuditStore _store;
    private readonly IAuditArchiveRepository _archive;
    private readonly AuditPolicyEvaluator _policies;
    private readonly AuditEngineOptions _options;

    /// <summary>Initializes a new instance of the <see cref="AuditArchiveManager"/> class.</summary>
    /// <param name="store">The hot store.</param>
    /// <param name="archive">The archive repository.</param>
    /// <param name="policies">The policy evaluator.</param>
    /// <param name="options">The engine options.</param>
    public AuditArchiveManager(
        IAuditStore store,
        IAuditArchiveRepository archive,
        AuditPolicyEvaluator policies,
        AuditEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(options);
        _store = store;
        _archive = archive;
        _policies = policies;
        _options = options;
    }

    /// <summary>Archives the records whose archive policy has come due.</summary>
    /// <param name="nowUtc">The current time.</param>
    /// <returns>The records that were archived.</returns>
    public IReadOnlyList<AuditRecord> ArchiveDue(DateTimeOffset nowUtc)
    {
        var due = _store.All()
            .Where(record => _policies.IsDueForArchive(record, nowUtc))
            .Take(_options.MaintenanceBatchSize)
            .ToArray();

        if (due.Length == 0)
        {
            return [];
        }

        _archive.Archive(due);
        _store.Remove(due.Select(record => record.Id));
        return due;
    }

    /// <summary>Brings archived records back into the hot store.</summary>
    /// <param name="tenant">The tenant whose archive is being restored from.</param>
    /// <param name="ids">The records to restore.</param>
    /// <returns>The records that were restored.</returns>
    public IReadOnlyList<AuditRecord> Restore(string tenant, IEnumerable<Guid> ids)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentNullException.ThrowIfNull(ids);

        var wanted = ids.ToHashSet();
        var restored = _archive.ListByTenant(tenant).Where(record => wanted.Contains(record.Id)).ToArray();
        if (restored.Length == 0)
        {
            return [];
        }

        foreach (var record in restored)
        {
            _store.Append(record);
        }

        _archive.Remove(restored.Select(record => record.Id));
        return restored;
    }

    /// <summary>Lists a tenant's archived records in chain order.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The archived records.</returns>
    public IReadOnlyList<AuditRecord> Archived(string tenant) => _archive.ListByTenant(tenant);
}

/// <summary>What one retention pass did.</summary>
/// <param name="Deleted">How many records were removed for good.</param>
/// <param name="Archived">How many records were archived instead of deleted, per their policy.</param>
public sealed record AuditRetentionSummary(int Deleted, int Archived);

/// <summary>
/// Applies retention: the end of a record's life. A record that has outlived its policy is either removed for
/// good or, when the policy says so, moved into the archive instead — which is what regulated categories need,
/// where nothing may ever be deleted but the hot trail still has to stay a workable size.
/// </summary>
public sealed class AuditRetentionManager
{
    private readonly IAuditStore _store;
    private readonly IAuditArchiveRepository _archive;
    private readonly AuditPolicyEvaluator _policies;
    private readonly AuditEngineOptions _options;

    /// <summary>Initializes a new instance of the <see cref="AuditRetentionManager"/> class.</summary>
    /// <param name="store">The hot store.</param>
    /// <param name="archive">The archive repository.</param>
    /// <param name="policies">The policy evaluator.</param>
    /// <param name="options">The engine options.</param>
    public AuditRetentionManager(
        IAuditStore store,
        IAuditArchiveRepository archive,
        AuditPolicyEvaluator policies,
        AuditEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(options);
        _store = store;
        _archive = archive;
        _policies = policies;
        _options = options;
    }

    /// <summary>Applies retention to a tenant's hot and archived records.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <returns>A summary of the pass.</returns>
    public AuditRetentionSummary Run(string tenant, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        var deleted = 0;
        var archived = 0;

        var hot = _store.ListByTenant(tenant).Take(_options.MaintenanceBatchSize).ToArray();
        var toDelete = new List<Guid>();
        var toArchive = new List<AuditRecord>();

        foreach (var record in hot)
        {
            if (_policies.ExpiredPolicyFor(record, nowUtc) is not { } policy)
            {
                continue;
            }

            if (policy.Action == AuditRetentionAction.Archive)
            {
                toArchive.Add(record);
            }
            else
            {
                toDelete.Add(record.Id);
            }
        }

        if (toArchive.Count > 0)
        {
            _archive.Archive(toArchive);
            _store.Remove(toArchive.Select(record => record.Id));
            archived += toArchive.Count;
        }

        if (toDelete.Count > 0)
        {
            deleted += _store.Remove(toDelete);
        }

        // Archived records reaching the end of a delete-policy retention leave for good.
        var expiredInArchive = _archive.ListByTenant(tenant)
            .Where(record => _policies.ExpiredPolicyFor(record, nowUtc) is { Action: AuditRetentionAction.Delete })
            .Select(record => record.Id)
            .ToArray();

        if (expiredInArchive.Length > 0)
        {
            deleted += _archive.Remove(expiredInArchive);
        }

        return new AuditRetentionSummary(deleted, archived);
    }
}
