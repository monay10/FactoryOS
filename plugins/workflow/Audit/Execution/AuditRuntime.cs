using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugins.Workflow.Audit.Configuration;
using FactoryOS.Plugins.Workflow.Audit.Diagnostics;
using FactoryOS.Plugins.Workflow.Audit.Domain;
using FactoryOS.Plugins.Workflow.Audit.Events;
using FactoryOS.Plugins.Workflow.Audit.Persistence;

namespace FactoryOS.Plugins.Workflow.Audit.Execution;

/// <summary>
/// The heart of the audit engine: it filters entries, seals the admitted ones into the tenant's hash chain,
/// verifies that chain on demand, and runs the archive, retention, search and export services over it. It reads
/// events from every engine above it and writes nothing back to any of them — audit is a one-way street by
/// construction.
/// </summary>
public sealed class AuditRuntime
{
    private readonly AuditRecorder _recorder;
    private readonly AuditFilter _filter;
    private readonly AuditChainVerifier _verifier;
    private readonly AuditArchiveManager _archives;
    private readonly AuditRetentionManager _retention;
    private readonly AuditSearchService _search;
    private readonly AuditExportService _export;
    private readonly AuditDispatcher _dispatcher;
    private readonly IAuditStore _store;
    private readonly AuditMetrics _metrics;
    private readonly AuditEngineOptions _options;
    private readonly IDateTimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="AuditRuntime"/> class.</summary>
    /// <param name="recorder">The recorder that seals entries into the chain.</param>
    /// <param name="filter">The entry filter.</param>
    /// <param name="verifier">The chain verifier.</param>
    /// <param name="archives">The archive manager.</param>
    /// <param name="retention">The retention manager.</param>
    /// <param name="search">The search service.</param>
    /// <param name="export">The export service.</param>
    /// <param name="dispatcher">The event dispatcher.</param>
    /// <param name="store">The hot store.</param>
    /// <param name="metrics">The metrics counters.</param>
    /// <param name="options">The engine options.</param>
    /// <param name="clock">The clock.</param>
    public AuditRuntime(
        AuditRecorder recorder,
        AuditFilter filter,
        AuditChainVerifier verifier,
        AuditArchiveManager archives,
        AuditRetentionManager retention,
        AuditSearchService search,
        AuditExportService export,
        AuditDispatcher dispatcher,
        IAuditStore store,
        AuditMetrics metrics,
        AuditEngineOptions options,
        IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(recorder);
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(verifier);
        ArgumentNullException.ThrowIfNull(archives);
        ArgumentNullException.ThrowIfNull(retention);
        ArgumentNullException.ThrowIfNull(search);
        ArgumentNullException.ThrowIfNull(export);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        _recorder = recorder;
        _filter = filter;
        _verifier = verifier;
        _archives = archives;
        _retention = retention;
        _search = search;
        _export = export;
        _dispatcher = dispatcher;
        _store = store;
        _metrics = metrics;
        _options = options;
        _clock = clock;
    }

    /// <summary>Records an entry, unless the filter rejects it.</summary>
    /// <param name="entry">The entry to record.</param>
    /// <returns>The sealed record, or <see langword="null"/> when the entry was filtered out.</returns>
    public AuditRecord? Record(AuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (!_filter.ShouldRecord(entry))
        {
            _metrics.RecordFiltered();
            return null;
        }

        var record = _recorder.Record(entry);
        _metrics.RecordRecorded();
        _dispatcher.Publish(new AuditRecorded(
            record.Tenant, record.RecordedOnUtc, record.Id, record.Sequence, record.Hash));
        return record;
    }

    /// <summary>Verifies a tenant's chain and reports the first place it breaks.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="includeArchived">Whether archived records are verified alongside the hot trail.</param>
    /// <returns>The verdict.</returns>
    public AuditChainVerification Verify(string tenant, bool includeArchived = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        var records = includeArchived
            ? _archives.Archived(tenant).Concat(_store.ListByTenant(tenant))
                .OrderBy(record => record.Sequence).ToArray()
            : _store.ListByTenant(tenant);

        var verification = _verifier.Verify(records);
        if (!verification.IsValid)
        {
            _metrics.RecordTamperDetection();
        }

        return verification;
    }

    /// <summary>Archives the records whose archive policy has come due.</summary>
    /// <returns>How many records were archived.</returns>
    public int ArchiveDue()
    {
        var archived = _archives.ArchiveDue(_clock.UtcNow);
        if (archived.Count == 0)
        {
            return 0;
        }

        _metrics.RecordArchived(archived.Count);
        foreach (var group in archived.GroupBy(record => record.Tenant, StringComparer.Ordinal))
        {
            _dispatcher.Publish(new AuditArchived(group.Key, _clock.UtcNow, group.Count()));
        }

        return archived.Count;
    }

    /// <summary>Applies retention to a tenant's records.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>A summary of the pass.</returns>
    public AuditRetentionSummary RunRetention(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        var now = _clock.UtcNow;
        var summary = _retention.Run(tenant, now);

        if (summary.Deleted > 0)
        {
            _metrics.RecordExpired(summary.Deleted);
            _dispatcher.Publish(new AuditRetentionExpired(tenant, now, summary.Deleted));
        }

        if (summary.Archived > 0)
        {
            _metrics.RecordArchived(summary.Archived);
            _dispatcher.Publish(new AuditArchived(tenant, now, summary.Archived));
        }

        return summary;
    }

    /// <summary>Restores archived records into the hot trail.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="ids">The records to restore.</param>
    /// <returns>How many records were restored.</returns>
    public int Restore(string tenant, IEnumerable<Guid> ids)
    {
        var restored = _archives.Restore(tenant, ids);
        if (restored.Count == 0)
        {
            return 0;
        }

        _metrics.RecordRestored(restored.Count);
        _dispatcher.Publish(new AuditRestored(tenant, _clock.UtcNow, restored.Count));
        return restored.Count;
    }

    /// <summary>Runs a search over the trail.</summary>
    /// <param name="query">The filters to apply.</param>
    /// <returns>The matching records.</returns>
    public IReadOnlyList<AuditRecord> Search(AuditQuery query)
    {
        _metrics.RecordSearch();
        return _search.Search(query);
    }

    /// <summary>Projects a tenant's sessions from the trail.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="includeArchived">Whether archived records contribute.</param>
    /// <returns>The sessions.</returns>
    public IReadOnlyList<AuditSession> Sessions(string tenant, bool includeArchived = false) =>
        _search.Sessions(tenant, includeArchived);

    /// <summary>
    /// Exports the records matching a query. The export is itself recorded when the engine is configured to
    /// audit its own operations — a trail that does not say who read it is not a complete trail.
    /// </summary>
    /// <param name="query">The filters selecting what to export.</param>
    /// <param name="format">The format to render.</param>
    /// <param name="exportedBy">Who is exporting.</param>
    /// <returns>The rendered export.</returns>
    public string Export(AuditQuery query, AuditExportFormat format, string exportedBy)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(exportedBy);

        var records = _search.Search(query);
        var rendered = _export.Export(records, format);
        _metrics.RecordExported(records.Count);
        _dispatcher.Publish(new AuditExported(
            query.Tenant, _clock.UtcNow, records.Count, format.ToString(), exportedBy));

        if (_options.AuditOwnOperations)
        {
            Record(new AuditEntry
            {
                Category = AuditCategory.System,
                Action = AuditAction.Exported,
                Target = new AuditTarget(AuditTargetType.Tenant, query.Tenant),
                Scope = AuditScope.ForTenant(query.Tenant),
                Actor = AuditActor.User(exportedBy),
                Severity = AuditSeverity.Notice,
                EventType = nameof(AuditExported),
                Message = $"{exportedBy} exported {records.Count} audit record(s) as {format}.",
            });
        }

        return rendered;
    }

    /// <summary>Gets a record by id.</summary>
    /// <param name="id">The record id.</param>
    /// <returns>The record, or <see langword="null"/> when not found.</returns>
    public AuditRecord? Get(Guid id) => _store.Get(id);

    /// <summary>Lists a tenant's hot records in chain order.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The records.</returns>
    public IReadOnlyList<AuditRecord> ListByTenant(string tenant) => _store.ListByTenant(tenant);

    /// <summary>Lists a tenant's archived records in chain order.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The archived records.</returns>
    public IReadOnlyList<AuditRecord> Archived(string tenant) => _archives.Archived(tenant);
}
