using FactoryOS.Persistence.Context;
using FactoryOS.Persistence.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace FactoryOS.Api.Persistence.Audit;

/// <summary>
/// The persistable state of one sealed audit record — the flat row the hot trail is stored as. It is deliberately
/// separate from the rich <c>AuditRecord</c> domain object: the value objects are flattened to columns (the scope,
/// actor, target and correlation each to their own set of columns; the metadata, snapshot and tags to JSON). The
/// two hash-covered timestamps are stored as round-trip (<c>"O"</c>) text rather than as database timestamps, so a
/// record reloads byte-for-byte identical on every provider and its <c>RecomputeHash</c> still matches its stored
/// <c>Hash</c> — persistence must never truncate a value the tamper-evident chain is computed over.
/// </summary>
public sealed class AuditRecordRow
{
    /// <summary>The record id.</summary>
    public Guid Id { get; set; }

    /// <summary>The owning tenant.</summary>
    public string Tenant { get; set; } = string.Empty;

    /// <summary>The record's position in its tenant's chain, starting at one.</summary>
    public long Sequence { get; set; }

    /// <summary>The organization or site the record is scoped to; null when tenant-wide.</summary>
    public string? ScopeOrganization { get; set; }

    /// <summary>The module or plugin key the record came from; null when none.</summary>
    public string? ScopeModule { get; set; }

    /// <summary>Which part of the platform the record came from, as its enum name.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>The verb describing what happened, as its enum name.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>How much attention the record deserves, as its enum name.</summary>
    public string Severity { get; set; } = string.Empty;

    /// <summary>Whether the operation succeeded, as its enum name.</summary>
    public string Result { get; set; } = string.Empty;

    /// <summary>The performing principal's id.</summary>
    public string ActorId { get; set; } = string.Empty;

    /// <summary>The performing principal's kind, as its enum name.</summary>
    public string ActorKind { get; set; } = string.Empty;

    /// <summary>The performing principal's human-readable name, when known.</summary>
    public string? ActorDisplayName { get; set; }

    /// <summary>The kind of entity the record is about, as its enum name.</summary>
    public string TargetType { get; set; } = string.Empty;

    /// <summary>The target entity's definition key or name.</summary>
    public string TargetKey { get; set; } = string.Empty;

    /// <summary>The target entity's instance id, when it has one.</summary>
    public string? TargetId { get; set; }

    /// <summary>The id grouping every record from one logical operation; null when none.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>The distributed trace the operation belonged to; null when none.</summary>
    public string? TraceId { get; set; }

    /// <summary>The user session the operation belonged to; null when none.</summary>
    public string? SessionId { get; set; }

    /// <summary>The inbound request that triggered the operation; null when none.</summary>
    public string? RequestId { get; set; }

    /// <summary>The specific event or command that caused this one; null when none.</summary>
    public string? CausationId { get; set; }

    /// <summary>The precise source event type name.</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>The human-readable description.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>The before-and-after state as JSON, for change records; null when the record has no snapshot.</summary>
    public string? SnapshotJson { get; set; }

    /// <summary>The additional key-value context, as a JSON object.</summary>
    public string MetadataJson { get; set; } = "{}";

    /// <summary>The labels used to slice the trail, as a JSON array of names.</summary>
    public string TagsJson { get; set; } = "[]";

    /// <summary>When the operation happened, as round-trip (<c>"O"</c>) text so the hash-covered value is exact.</summary>
    public string OccurredOnUtc { get; set; } = string.Empty;

    /// <summary>When the audit engine recorded it, as round-trip (<c>"O"</c>) text.</summary>
    public string RecordedOnUtc { get; set; } = string.Empty;

    /// <summary>The hash of the record before this one in the tenant's chain.</summary>
    public string PreviousHash { get; set; } = string.Empty;

    /// <summary>The hash over this record's content and <see cref="PreviousHash"/>.</summary>
    public string Hash { get; set; } = string.Empty;
}

/// <summary>
/// The audit engine's persistence context — the hot, append-only trail persisted to the configured relational
/// database. It inherits the FactoryOS schema-per-tenant and convention pipeline from <see cref="FactoryOsDbContext"/>
/// and maps a single table of sealed audit records.
/// </summary>
public sealed class AuditDbContext : FactoryOsDbContext
{
    /// <summary>Initializes a new instance of the <see cref="AuditDbContext"/> class.</summary>
    /// <param name="options">The context options.</param>
    /// <param name="schemaProvider">The tenant schema provider.</param>
    public AuditDbContext(DbContextOptions<AuditDbContext> options, ITenantSchemaProvider schemaProvider)
        : base(options, schemaProvider)
    {
    }

    /// <summary>The sealed audit records that make up the hot trail.</summary>
    public DbSet<AuditRecordRow> Records => Set<AuditRecordRow>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<AuditRecordRow>(entity =>
        {
            entity.ToTable("audit_records");
            entity.HasKey(record => record.Id);

            // A record's position in its tenant's chain is unique by construction; the index also serves the
            // by-tenant, in-sequence reads the store makes (list, head).
            entity.HasIndex(record => new { record.Tenant, record.Sequence }).IsUnique();

            entity.Property(record => record.Tenant).IsRequired();
            entity.Property(record => record.Category).IsRequired();
            entity.Property(record => record.Action).IsRequired();
            entity.Property(record => record.Severity).IsRequired();
            entity.Property(record => record.Result).IsRequired();
            entity.Property(record => record.ActorId).IsRequired();
            entity.Property(record => record.ActorKind).IsRequired();
            entity.Property(record => record.TargetType).IsRequired();
            entity.Property(record => record.TargetKey).IsRequired();
            entity.Property(record => record.MetadataJson).IsRequired();
            entity.Property(record => record.TagsJson).IsRequired();
            entity.Property(record => record.OccurredOnUtc).IsRequired();
            entity.Property(record => record.RecordedOnUtc).IsRequired();
            entity.Property(record => record.PreviousHash).IsRequired();
            entity.Property(record => record.Hash).IsRequired();
        });

        // Base conventions (schema, soft-delete filter, concurrency token) applied last.
        base.OnModelCreating(modelBuilder);
    }
}
