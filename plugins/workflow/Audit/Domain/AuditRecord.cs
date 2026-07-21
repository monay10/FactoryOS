using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace FactoryOS.Plugins.Workflow.Audit.Domain;

/// <summary>
/// A sealed, immutable audit record. Every property is get-only and there is no mutator anywhere on the type,
/// so a record cannot be altered once it exists. Each record carries a per-tenant <see cref="Sequence"/>, the
/// <see cref="PreviousHash"/> of the record before it and its own <see cref="Hash"/> over its content — that
/// chain is what makes tampering detectable: changing any field, or removing or reordering any record, breaks
/// the link and <see cref="RecomputeHash"/> stops matching <see cref="Hash"/>.
/// <para>
/// Records are created in exactly two ways: <see cref="Seal"/>, which computes the hash for a new record, and
/// <see cref="Rehydrate"/>, which reconstructs one from storage with the hash it was stored with. Storage must
/// never recompute a hash it reads, or tampering would be silently repaired.
/// </para>
/// </summary>
public sealed class AuditRecord
{
    /// <summary>The <see cref="PreviousHash"/> of the first record in a tenant's chain.</summary>
    public const string GenesisHash = "GENESIS";

    private readonly Dictionary<string, string> _metadata;

    private AuditRecord(
        Guid id,
        long sequence,
        AuditEntry entry,
        DateTimeOffset occurredOnUtc,
        DateTimeOffset recordedOnUtc,
        string previousHash,
        string hash)
    {
        Id = id;
        Sequence = sequence;
        Category = entry.Category;
        Action = entry.Action;
        Severity = entry.Severity;
        Result = entry.Result;
        Actor = entry.Actor;
        Target = entry.Target;
        Scope = entry.Scope;
        Correlation = entry.Correlation;
        EventType = entry.EventType;
        Message = entry.Message;
        Snapshot = entry.Snapshot;
        OccurredOnUtc = occurredOnUtc;
        RecordedOnUtc = recordedOnUtc;
        PreviousHash = previousHash;
        Hash = hash;
        _metadata = new Dictionary<string, string>(entry.Metadata, StringComparer.Ordinal);
        Tags = [.. entry.Tags];
    }

    /// <summary>Gets the record id.</summary>
    public Guid Id { get; }

    /// <summary>Gets the record's position in its tenant's chain, starting at one.</summary>
    public long Sequence { get; }

    /// <summary>Gets the owning tenant.</summary>
    public string Tenant => Scope.Tenant;

    /// <summary>Gets which part of the platform the record came from.</summary>
    public AuditCategory Category { get; }

    /// <summary>Gets the verb describing what happened.</summary>
    public AuditAction Action { get; }

    /// <summary>Gets how much attention the record deserves.</summary>
    public AuditSeverity Severity { get; }

    /// <summary>Gets whether the operation succeeded.</summary>
    public AuditResult Result { get; }

    /// <summary>Gets who performed the operation.</summary>
    public AuditActor Actor { get; }

    /// <summary>Gets what the record is about.</summary>
    public AuditTarget Target { get; }

    /// <summary>Gets the breadth the record belongs to.</summary>
    public AuditScope Scope { get; }

    /// <summary>Gets the identifiers tying the record to its request, trace and session.</summary>
    public AuditCorrelation Correlation { get; }

    /// <summary>Gets the precise source event type name.</summary>
    public string EventType { get; }

    /// <summary>Gets the human-readable description.</summary>
    public string Message { get; }

    /// <summary>Gets the before-and-after state, for records that describe a change.</summary>
    public AuditSnapshot? Snapshot { get; }

    /// <summary>Gets when the operation happened.</summary>
    public DateTimeOffset OccurredOnUtc { get; }

    /// <summary>Gets when the audit engine recorded it.</summary>
    public DateTimeOffset RecordedOnUtc { get; }

    /// <summary>Gets additional key-value context.</summary>
    public IReadOnlyDictionary<string, string> Metadata => _metadata;

    /// <summary>Gets the labels used to slice the trail.</summary>
    public IReadOnlyList<AuditTag> Tags { get; }

    /// <summary>Gets the hash of the record before this one in the tenant's chain.</summary>
    public string PreviousHash { get; }

    /// <summary>Gets the hash over this record's content and <see cref="PreviousHash"/>.</summary>
    public string Hash { get; }

    /// <summary>Seals an entry into an immutable record, linking it to the tenant's chain.</summary>
    /// <param name="entry">The entry to seal.</param>
    /// <param name="sequence">The record's position in the tenant's chain.</param>
    /// <param name="previousHash">The hash of the preceding record, or <see cref="GenesisHash"/> for the first.</param>
    /// <param name="recordedOnUtc">When the audit engine recorded it.</param>
    /// <returns>The sealed record.</returns>
    public static AuditRecord Seal(
        AuditEntry entry, long sequence, string previousHash, DateTimeOffset recordedOnUtc)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(previousHash);

        var id = Guid.NewGuid();
        var occurredOn = entry.OccurredOnUtc ?? recordedOnUtc;
        var hash = ComputeHash(sequence, entry, occurredOn, previousHash);
        return new AuditRecord(id, sequence, entry, occurredOn, recordedOnUtc, previousHash, hash);
    }

    /// <summary>
    /// Reconstructs a record from storage with the hash it was stored with. The hash is taken as read and never
    /// recomputed here — that is exactly what allows <see cref="RecomputeHash"/> to expose a tampered row.
    /// </summary>
    /// <param name="id">The record id.</param>
    /// <param name="sequence">The record's position in the tenant's chain.</param>
    /// <param name="entry">The record's content.</param>
    /// <param name="occurredOnUtc">When the operation happened.</param>
    /// <param name="recordedOnUtc">When it was recorded.</param>
    /// <param name="previousHash">The stored previous hash.</param>
    /// <param name="hash">The stored hash.</param>
    /// <returns>The reconstructed record.</returns>
    public static AuditRecord Rehydrate(
        Guid id,
        long sequence,
        AuditEntry entry,
        DateTimeOffset occurredOnUtc,
        DateTimeOffset recordedOnUtc,
        string previousHash,
        string hash)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(previousHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        return new AuditRecord(id, sequence, entry, occurredOnUtc, recordedOnUtc, previousHash, hash);
    }

    /// <summary>Recomputes the hash this record's content implies, for tamper detection.</summary>
    /// <returns>The hash the content implies; it differs from <see cref="Hash"/> when the record was altered.</returns>
    public string RecomputeHash() => ComputeHash(
        Sequence,
        new AuditEntry
        {
            Category = Category,
            Action = Action,
            Target = Target,
            Scope = Scope,
            Actor = Actor,
            Severity = Severity,
            Result = Result,
            Correlation = Correlation,
            EventType = EventType,
            Message = Message,
            Snapshot = Snapshot,
            Metadata = _metadata,
            Tags = Tags,
        },
        OccurredOnUtc,
        PreviousHash);

    private static string ComputeHash(
        long sequence, AuditEntry entry, DateTimeOffset occurredOnUtc, string previousHash)
    {
        // A single canonical rendering, so the same content always hashes to the same value on every host.
        var builder = new StringBuilder(512);
        builder.Append(sequence.ToString(CultureInfo.InvariantCulture)).Append('|')
            .Append(entry.Scope.Tenant).Append('|')
            .Append(entry.Scope.Organization).Append('|')
            .Append(entry.Scope.Module).Append('|')
            .Append(occurredOnUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)).Append('|')
            .Append(entry.Category).Append('|')
            .Append(entry.Action).Append('|')
            .Append(entry.Severity).Append('|')
            .Append(entry.Result).Append('|')
            .Append(entry.Actor.Kind).Append(':').Append(entry.Actor.Id).Append('|')
            .Append(entry.Target.Type).Append(':').Append(entry.Target.Key).Append(':').Append(entry.Target.Id).Append('|')
            .Append(entry.Correlation.CorrelationId).Append('|')
            .Append(entry.Correlation.TraceId).Append('|')
            .Append(entry.Correlation.SessionId).Append('|')
            .Append(entry.Correlation.RequestId).Append('|')
            .Append(entry.Correlation.CausationId).Append('|')
            .Append(entry.EventType).Append('|')
            .Append(entry.Message).Append('|');

        foreach (var pair in entry.Metadata.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            builder.Append(pair.Key).Append('=').Append(pair.Value).Append(';');
        }

        builder.Append('|');
        foreach (var tag in entry.Tags.Select(tag => tag.Name).OrderBy(name => name, StringComparer.Ordinal))
        {
            builder.Append(tag).Append(';');
        }

        builder.Append('|')
            .Append(entry.Snapshot?.ToCanonicalString())
            .Append('|')
            .Append(previousHash);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }
}
