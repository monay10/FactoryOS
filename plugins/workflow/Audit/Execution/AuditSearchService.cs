using FactoryOS.Plugins.Workflow.Audit.Domain;
using FactoryOS.Plugins.Workflow.Audit.Persistence;

namespace FactoryOS.Plugins.Workflow.Audit.Execution;

/// <summary>
/// The filters a search applies. Every filter is optional and they combine with AND; the tenant is required so
/// no query can ever reach across tenants by omission.
/// </summary>
public sealed record AuditQuery
{
    /// <summary>Gets the tenant to search within.</summary>
    public required string Tenant { get; init; }

    /// <summary>Gets the category to match.</summary>
    public AuditCategory? Category { get; init; }

    /// <summary>Gets the action to match.</summary>
    public AuditAction? Action { get; init; }

    /// <summary>Gets the lowest severity to include.</summary>
    public AuditSeverity? MinimumSeverity { get; init; }

    /// <summary>Gets the result to match.</summary>
    public AuditResult? Result { get; init; }

    /// <summary>Gets the actor id to match.</summary>
    public string? ActorId { get; init; }

    /// <summary>Gets the target type to match.</summary>
    public AuditTargetType? TargetType { get; init; }

    /// <summary>Gets the target key to match.</summary>
    public string? TargetKey { get; init; }

    /// <summary>Gets the target instance id to match.</summary>
    public string? TargetId { get; init; }

    /// <summary>Gets the correlation id to match — the way to pull one operation's whole trail together.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>Gets the distributed trace id to match.</summary>
    public string? TraceId { get; init; }

    /// <summary>Gets the session id to match.</summary>
    public string? SessionId { get; init; }

    /// <summary>Gets the organization to match.</summary>
    public string? Organization { get; init; }

    /// <summary>Gets the earliest occurrence time to include.</summary>
    public DateTimeOffset? FromUtc { get; init; }

    /// <summary>Gets the latest occurrence time to include.</summary>
    public DateTimeOffset? ToUtc { get; init; }

    /// <summary>Gets a tag that must be present.</summary>
    public string? Tag { get; init; }

    /// <summary>Gets text that must appear in the message (case-insensitive).</summary>
    public string? MessageContains { get; init; }

    /// <summary>Gets a value indicating whether archived records are searched as well as the hot trail.</summary>
    public bool IncludeArchived { get; init; }

    /// <summary>Gets the maximum number of records to return.</summary>
    public int Limit { get; init; } = 500;
}

/// <summary>
/// Queries the audit trail. Searches read from the hot store and, when asked, from the archive too, so a
/// correlation id still pulls a whole operation together after part of it has aged out of the hot trail. It
/// also projects sessions from the records that share a session id — a projection, never a stored entity, so it
/// can never drift from the trail it summarises.
/// </summary>
public sealed class AuditSearchService
{
    private readonly IAuditStore _store;
    private readonly IAuditArchiveRepository _archive;

    /// <summary>Initializes a new instance of the <see cref="AuditSearchService"/> class.</summary>
    /// <param name="store">The hot store.</param>
    /// <param name="archive">The archive repository.</param>
    public AuditSearchService(IAuditStore store, IAuditArchiveRepository archive)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(archive);
        _store = store;
        _archive = archive;
    }

    /// <summary>Runs a search.</summary>
    /// <param name="query">The filters to apply.</param>
    /// <returns>The matching records, in chain order.</returns>
    public IReadOnlyList<AuditRecord> Search(AuditQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.Tenant);

        IEnumerable<AuditRecord> records = _store.ListByTenant(query.Tenant);
        if (query.IncludeArchived)
        {
            records = records.Concat(_archive.ListByTenant(query.Tenant));
        }

        return records
            .Where(record => Matches(record, query))
            .OrderBy(record => record.Sequence)
            .Take(Math.Max(0, query.Limit))
            .ToArray();
    }

    /// <summary>Projects a tenant's sessions from the records that carry a session id.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="includeArchived">Whether archived records contribute to the projection.</param>
    /// <returns>The sessions, most recent first.</returns>
    public IReadOnlyList<AuditSession> Sessions(string tenant, bool includeArchived = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        IEnumerable<AuditRecord> records = _store.ListByTenant(tenant);
        if (includeArchived)
        {
            records = records.Concat(_archive.ListByTenant(tenant));
        }

        return records
            .Where(record => !string.IsNullOrWhiteSpace(record.Correlation.SessionId))
            .GroupBy(record => record.Correlation.SessionId!, StringComparer.Ordinal)
            .Select(group => new AuditSession(
                group.Key,
                tenant,
                group.OrderBy(record => record.Sequence).First().Actor,
                group.Min(record => record.OccurredOnUtc),
                group.Max(record => record.OccurredOnUtc),
                group.Count()))
            .OrderByDescending(session => session.StartedOnUtc)
            .ToArray();
    }

    private static bool Matches(AuditRecord record, AuditQuery query)
    {
        if (query.Category is { } category && record.Category != category)
        {
            return false;
        }

        if (query.Action is { } action && record.Action != action)
        {
            return false;
        }

        if (query.MinimumSeverity is { } severity && record.Severity < severity)
        {
            return false;
        }

        if (query.Result is { } result && record.Result != result)
        {
            return false;
        }

        if (query.ActorId is { } actorId && !string.Equals(record.Actor.Id, actorId, StringComparison.Ordinal))
        {
            return false;
        }

        if (query.TargetType is { } targetType && record.Target.Type != targetType)
        {
            return false;
        }

        if (query.TargetKey is { } targetKey && !string.Equals(record.Target.Key, targetKey, StringComparison.Ordinal))
        {
            return false;
        }

        if (query.TargetId is { } targetId && !string.Equals(record.Target.Id, targetId, StringComparison.Ordinal))
        {
            return false;
        }

        if (query.CorrelationId is { } correlationId
            && !string.Equals(record.Correlation.CorrelationId, correlationId, StringComparison.Ordinal))
        {
            return false;
        }

        if (query.TraceId is { } traceId
            && !string.Equals(record.Correlation.TraceId, traceId, StringComparison.Ordinal))
        {
            return false;
        }

        if (query.SessionId is { } sessionId
            && !string.Equals(record.Correlation.SessionId, sessionId, StringComparison.Ordinal))
        {
            return false;
        }

        if (query.Organization is { } organization
            && !string.Equals(record.Scope.Organization, organization, StringComparison.Ordinal))
        {
            return false;
        }

        if (query.FromUtc is { } from && record.OccurredOnUtc < from)
        {
            return false;
        }

        if (query.ToUtc is { } to && record.OccurredOnUtc > to)
        {
            return false;
        }

        if (query.Tag is { } tag
            && !record.Tags.Any(candidate => string.Equals(candidate.Name, tag, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (query.MessageContains is { } text
            && !record.Message.Contains(text, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
