using System.Globalization;
using System.Text.Json;
using FactoryOS.Plugins.Workflow.Audit.Domain;
using FactoryOS.Plugins.Workflow.Audit.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactoryOS.Api.Persistence.Audit;

/// <summary>
/// An EF Core <see cref="IAuditStore"/> — the hot, append-only audit trail persisted to the configured relational
/// database. It is the adapter a host swaps in for the engine's in-memory default when a database is configured, so
/// the immutable record of everything the platform did survives a restart.
/// <para>
/// The store is a singleton (the audit engine resolves it as one), so it holds an
/// <see cref="IDbContextFactory{TContext}"/> and opens a short-lived context per operation rather than capturing a
/// scoped one. Mapping to and from the domain is explicit: a sealed <see cref="AuditRecord"/> is flattened to an
/// <see cref="AuditRecordRow"/> and reconstructed through <see cref="AuditRecord.Rehydrate"/> with the hash it was
/// stored with — never recomputed on read, so a tampered row stays detectable.
/// </para>
/// </summary>
public sealed class EfAuditStore : IAuditStore
{
    private readonly IDbContextFactory<AuditDbContext> _factory;

    /// <summary>Initializes the store and ensures its schema exists.</summary>
    /// <param name="factory">The context factory the store opens a context from per operation.</param>
    public EfAuditStore(IDbContextFactory<AuditDbContext> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;

        // Create the audit table on first construction. Idempotent, so a second store over the same database is a
        // no-op. Real Postgres migrations are a later concern; this makes the store self-sufficient.
        using var context = _factory.CreateDbContext();
        context.Database.EnsureCreated();
    }

    /// <inheritdoc />
    public void Append(AuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        using var context = _factory.CreateDbContext();
        var row = ToRow(record);

        // At-least-once delivery means a record may be re-appended; dedupe by id, mirroring the in-memory store.
        var existing = context.Records.Find(record.Id);
        if (existing is null)
        {
            context.Records.Add(row);
        }
        else
        {
            context.Entry(existing).CurrentValues.SetValues(row);
        }

        context.SaveChanges();
    }

    /// <inheritdoc />
    public AuditRecord? Head(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        using var context = _factory.CreateDbContext();
        var row = context.Records.AsNoTracking()
            .Where(record => record.Tenant == tenant)
            .OrderByDescending(record => record.Sequence)
            .FirstOrDefault();
        return row is null ? null : ToDomain(row);
    }

    /// <inheritdoc />
    public AuditRecord? Get(Guid id)
    {
        using var context = _factory.CreateDbContext();
        var row = context.Records.AsNoTracking().FirstOrDefault(record => record.Id == id);
        return row is null ? null : ToDomain(row);
    }

    /// <inheritdoc />
    public IReadOnlyList<AuditRecord> ListByTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        using var context = _factory.CreateDbContext();
        return [.. context.Records.AsNoTracking()
            .Where(record => record.Tenant == tenant)
            .OrderBy(record => record.Sequence)
            .ToList()
            .Select(ToDomain)];
    }

    /// <inheritdoc />
    public IReadOnlyList<AuditRecord> All()
    {
        using var context = _factory.CreateDbContext();
        return [.. context.Records.AsNoTracking()
            .OrderBy(record => record.Tenant)
            .ThenBy(record => record.Sequence)
            .ToList()
            .Select(ToDomain)];
    }

    /// <inheritdoc />
    public int Remove(IEnumerable<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var wanted = ids.ToHashSet();
        if (wanted.Count == 0)
        {
            return 0;
        }

        using var context = _factory.CreateDbContext();
        var rows = context.Records.Where(record => wanted.Contains(record.Id)).ToList();
        context.Records.RemoveRange(rows);
        context.SaveChanges();
        return rows.Count;
    }

    private static AuditRecordRow ToRow(AuditRecord record) => new()
    {
        Id = record.Id,
        Tenant = record.Tenant,
        Sequence = record.Sequence,
        ScopeOrganization = record.Scope.Organization,
        ScopeModule = record.Scope.Module,
        Category = record.Category.ToString(),
        Action = record.Action.ToString(),
        Severity = record.Severity.ToString(),
        Result = record.Result.ToString(),
        ActorId = record.Actor.Id,
        ActorKind = record.Actor.Kind.ToString(),
        ActorDisplayName = record.Actor.DisplayName,
        TargetType = record.Target.Type.ToString(),
        TargetKey = record.Target.Key,
        TargetId = record.Target.Id,
        CorrelationId = record.Correlation.CorrelationId,
        TraceId = record.Correlation.TraceId,
        SessionId = record.Correlation.SessionId,
        RequestId = record.Correlation.RequestId,
        CausationId = record.Correlation.CausationId,
        EventType = record.EventType,
        Message = record.Message,
        SnapshotJson = record.Snapshot is null
            ? null
            : JsonSerializer.Serialize(new SnapshotDto(
                new Dictionary<string, string?>(record.Snapshot.Before, StringComparer.Ordinal),
                new Dictionary<string, string?>(record.Snapshot.After, StringComparer.Ordinal))),
        MetadataJson = JsonSerializer.Serialize(record.Metadata.Values),
        TagsJson = JsonSerializer.Serialize(record.Tags.Select(tag => tag.Name)),
        OccurredOnUtc = record.OccurredOnUtc.ToString("O", CultureInfo.InvariantCulture),
        RecordedOnUtc = record.RecordedOnUtc.ToString("O", CultureInfo.InvariantCulture),
        PreviousHash = record.PreviousHash,
        Hash = record.Hash,
    };

    private static AuditRecord ToDomain(AuditRecordRow row)
    {
        var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(row.MetadataJson)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
        var tags = (JsonSerializer.Deserialize<List<string>>(row.TagsJson) ?? [])
            .Select(name => new AuditTag(name))
            .ToArray();

        AuditSnapshot? snapshot = null;
        if (!string.IsNullOrEmpty(row.SnapshotJson))
        {
            var dto = JsonSerializer.Deserialize<SnapshotDto>(row.SnapshotJson);
            if (dto is not null)
            {
                snapshot = new AuditSnapshot(dto.Before, dto.After);
            }
        }

        var entry = new AuditEntry
        {
            Category = Enum.Parse<AuditCategory>(row.Category),
            Action = Enum.Parse<AuditAction>(row.Action),
            Severity = Enum.Parse<AuditSeverity>(row.Severity),
            Result = Enum.Parse<AuditResult>(row.Result),
            Actor = new AuditActor(row.ActorId, Enum.Parse<AuditActorKind>(row.ActorKind), row.ActorDisplayName),
            Target = new AuditTarget(Enum.Parse<AuditTargetType>(row.TargetType), row.TargetKey, row.TargetId),
            Scope = new AuditScope(row.Tenant, row.ScopeOrganization, row.ScopeModule),
            Correlation = new AuditCorrelation(
                row.CorrelationId, row.TraceId, row.SessionId, row.RequestId, row.CausationId),
            EventType = row.EventType,
            Message = row.Message,
            Snapshot = snapshot,
            Metadata = new AuditMetadata(metadata),
            Tags = tags,
        };

        return AuditRecord.Rehydrate(
            row.Id,
            row.Sequence,
            entry,
            DateTimeOffset.Parse(row.OccurredOnUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            DateTimeOffset.Parse(row.RecordedOnUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            row.PreviousHash,
            row.Hash);
    }

    /// <summary>The JSON shape of a persisted snapshot's before-and-after state.</summary>
    /// <param name="Before">The state before the change.</param>
    /// <param name="After">The state after the change.</param>
    private sealed record SnapshotDto(
        Dictionary<string, string?> Before,
        Dictionary<string, string?> After);
}
