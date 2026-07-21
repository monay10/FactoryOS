using System.Collections.Concurrent;

namespace FactoryOS.Plugins.Workflow.Audit.Events;

/// <summary>The base of an audit lifecycle event raised by the runtime and published onto the event bus.</summary>
/// <param name="Tenant">The owning tenant.</param>
/// <param name="OccurredOnUtc">When the event occurred.</param>
public abstract record AuditEvent(string Tenant, DateTimeOffset OccurredOnUtc);

/// <summary>Raised when an entry has been sealed into an immutable record and appended to the chain.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="RecordId">The sealed record.</param>
/// <param name="Sequence">Its position in the tenant's chain.</param>
/// <param name="Hash">Its hash.</param>
public sealed record AuditRecorded(
    string Tenant, DateTimeOffset OccurredOnUtc, Guid RecordId, long Sequence, string Hash)
    : AuditEvent(Tenant, OccurredOnUtc);

/// <summary>Raised when records move out of the hot store into the archive.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="Count">How many records were archived.</param>
public sealed record AuditArchived(string Tenant, DateTimeOffset OccurredOnUtc, int Count)
    : AuditEvent(Tenant, OccurredOnUtc);

/// <summary>Raised when records outlive their retention period and are removed for good.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="Count">How many records expired.</param>
public sealed record AuditRetentionExpired(string Tenant, DateTimeOffset OccurredOnUtc, int Count)
    : AuditEvent(Tenant, OccurredOnUtc);

/// <summary>Raised when records are exported — itself an audited act.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="Count">How many records were exported.</param>
/// <param name="Format">The export format.</param>
/// <param name="ExportedBy">Who exported them.</param>
public sealed record AuditExported(
    string Tenant, DateTimeOffset OccurredOnUtc, int Count, string Format, string ExportedBy)
    : AuditEvent(Tenant, OccurredOnUtc);

/// <summary>Raised when archived records are brought back into the hot store.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="Count">How many records were restored.</param>
public sealed record AuditRestored(string Tenant, DateTimeOffset OccurredOnUtc, int Count)
    : AuditEvent(Tenant, OccurredOnUtc);

/// <summary>
/// Receives audit lifecycle events raised by the runtime — the seam onto the platform event bus. Like the SLA
/// engine's seam this one fans out: the runtime publishes to every registered sink, so several consumers can
/// observe the same stream without displacing each other.
/// </summary>
public interface IAuditEventSink
{
    /// <summary>Publishes an audit event.</summary>
    /// <param name="auditEvent">The event to publish.</param>
    void Publish(AuditEvent auditEvent);
}

/// <summary>An in-memory <see cref="IAuditEventSink"/> that records published events for inspection.</summary>
public sealed class InMemoryAuditEventSink : IAuditEventSink
{
    private readonly ConcurrentQueue<AuditEvent> _events = new();

    /// <summary>Gets the published events in order.</summary>
    public IReadOnlyList<AuditEvent> Events => _events.ToArray();

    /// <inheritdoc />
    public void Publish(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        _events.Enqueue(auditEvent);
    }
}
