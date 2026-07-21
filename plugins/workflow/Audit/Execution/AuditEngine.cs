using FactoryOS.Plugins.Workflow.Audit.Diagnostics;
using FactoryOS.Plugins.Workflow.Audit.Domain;
using FactoryOS.Plugins.Workflow.Audit.Persistence;

namespace FactoryOS.Plugins.Workflow.Audit.Execution;

/// <summary>
/// The public entry point to the audit engine. It records entries into an immutable, hash-chained trail;
/// verifies that chain for tampering; registers retention and archive policies and runs them; searches,
/// projects sessions and exports; and reports its counters.
/// <para>
/// Audit sits at the bottom of the stack. It consumes the events every engine above it publishes and writes
/// nothing back to any of them — no engine references the audit namespace, and none was modified to be audited.
/// </para>
/// </summary>
public sealed class AuditEngine
{
    private readonly AuditRuntime _runtime;
    private readonly IAuditRepository _policies;
    private readonly AuditPermissionEvaluator _permissions;
    private readonly AuditMetrics _metrics;

    /// <summary>Initializes a new instance of the <see cref="AuditEngine"/> class.</summary>
    /// <param name="runtime">The audit runtime.</param>
    /// <param name="policies">The policy repository.</param>
    /// <param name="permissions">The permission evaluator.</param>
    /// <param name="metrics">The metrics counters.</param>
    public AuditEngine(
        AuditRuntime runtime,
        IAuditRepository policies,
        AuditPermissionEvaluator permissions,
        AuditMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(metrics);
        _runtime = runtime;
        _policies = policies;
        _permissions = permissions;
        _metrics = metrics;
    }

    /// <summary>Records an entry into the trail, unless the filter rejects it.</summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The sealed record, or <see langword="null"/> when it was filtered out.</returns>
    public AuditRecord? Record(AuditEntry entry) => _runtime.Record(entry);

    /// <summary>Registers a retention policy.</summary>
    /// <param name="policy">The policy.</param>
    public void RegisterRetention(AuditRetentionPolicy policy) => _policies.RegisterRetention(policy);

    /// <summary>Registers an archive policy.</summary>
    /// <param name="policy">The policy.</param>
    public void RegisterArchive(AuditArchivePolicy policy) => _policies.RegisterArchive(policy);

    /// <summary>Verifies a tenant's chain and reports the first place it breaks.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="includeArchived">Whether archived records are verified too.</param>
    /// <returns>The verdict.</returns>
    public AuditChainVerification Verify(string tenant, bool includeArchived = false) =>
        _runtime.Verify(tenant, includeArchived);

    /// <summary>Archives the records whose archive policy has come due.</summary>
    /// <returns>How many records were archived.</returns>
    public int ArchiveDue() => _runtime.ArchiveDue();

    /// <summary>Applies retention to a tenant's records.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>A summary of the pass.</returns>
    public AuditRetentionSummary RunRetention(string tenant) => _runtime.RunRetention(tenant);

    /// <summary>Restores archived records into the hot trail.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="ids">The records to restore.</param>
    /// <returns>How many records were restored.</returns>
    public int Restore(string tenant, IEnumerable<Guid> ids) => _runtime.Restore(tenant, ids);

    /// <summary>Runs a search over the trail.</summary>
    /// <param name="query">The filters to apply.</param>
    /// <returns>The matching records.</returns>
    public IReadOnlyList<AuditRecord> Search(AuditQuery query) => _runtime.Search(query);

    /// <summary>Projects a tenant's sessions from the trail.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="includeArchived">Whether archived records contribute.</param>
    /// <returns>The sessions.</returns>
    public IReadOnlyList<AuditSession> Sessions(string tenant, bool includeArchived = false) =>
        _runtime.Sessions(tenant, includeArchived);

    /// <summary>Exports the records matching a query, recording the export itself.</summary>
    /// <param name="query">The filters selecting what to export.</param>
    /// <param name="format">The format to render.</param>
    /// <param name="exportedBy">Who is exporting.</param>
    /// <returns>The rendered export.</returns>
    public string Export(AuditQuery query, AuditExportFormat format, string exportedBy) =>
        _runtime.Export(query, format, exportedBy);

    /// <summary>Gets a record by id.</summary>
    /// <param name="id">The record id.</param>
    /// <returns>The record, or <see langword="null"/> when not found.</returns>
    public AuditRecord? GetRecord(Guid id) => _runtime.Get(id);

    /// <summary>Lists a tenant's hot records in chain order.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The records.</returns>
    public IReadOnlyList<AuditRecord> ListByTenant(string tenant) => _runtime.ListByTenant(tenant);

    /// <summary>Lists a tenant's archived records in chain order.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The archived records.</returns>
    public IReadOnlyList<AuditRecord> Archived(string tenant) => _runtime.Archived(tenant);

    /// <summary>Gets a value indicating whether a principal holds an audit right.</summary>
    /// <param name="permission">The right to test.</param>
    /// <param name="principals">The principal and any roles or groups it belongs to.</param>
    /// <returns><see langword="true"/> when the right is held.</returns>
    public bool Allows(AuditPermission permission, params string[] principals) =>
        _permissions.Allows(permission, principals);

    /// <summary>Reads the engine's counters as a snapshot.</summary>
    /// <returns>The snapshot.</returns>
    public AuditMetricsSnapshot Snapshot() => _metrics.Snapshot();
}
