namespace FactoryOS.Plugins.Workflow.Audit.Domain;

/// <summary>
/// How long audit records are kept before they are removed for good. A policy may apply to one category or to
/// everything; the most specific match wins. Retention is the last step of a record's life — archiving comes
/// first, so an expired record is one that has already outlived even its archive.
/// </summary>
/// <param name="RetainFor">How long a record is kept, measured from when it was recorded.</param>
/// <param name="Action">What happens when the period passes.</param>
/// <param name="Category">The category the policy applies to, or <see langword="null"/> for all of them.</param>
public sealed record AuditRetentionPolicy(
    TimeSpan RetainFor,
    AuditRetentionAction Action = AuditRetentionAction.Delete,
    AuditCategory? Category = null)
{
    /// <summary>Gets a value indicating whether the policy applies to a record.</summary>
    /// <param name="record">The record.</param>
    /// <returns><see langword="true"/> when the policy covers the record's category.</returns>
    public bool Covers(AuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return Category is null || Category == record.Category;
    }

    /// <summary>Gets a value indicating whether a record has outlived the policy.</summary>
    /// <param name="record">The record.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <returns><see langword="true"/> when the retention period has passed.</returns>
    public bool HasExpired(AuditRecord record, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.RecordedOnUtc + RetainFor <= nowUtc;
    }
}

/// <summary>
/// When audit records move out of the hot store into the archive. Archived records stay fully readable and keep
/// their hashes, so an archived segment can still be verified — archiving is a storage decision, never a
/// deletion.
/// </summary>
/// <param name="ArchiveAfter">How long a record stays in the hot store, measured from when it was recorded.</param>
/// <param name="Category">The category the policy applies to, or <see langword="null"/> for all of them.</param>
public sealed record AuditArchivePolicy(TimeSpan ArchiveAfter, AuditCategory? Category = null)
{
    /// <summary>Gets a value indicating whether the policy applies to a record.</summary>
    /// <param name="record">The record.</param>
    /// <returns><see langword="true"/> when the policy covers the record's category.</returns>
    public bool Covers(AuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return Category is null || Category == record.Category;
    }

    /// <summary>Gets a value indicating whether a record is due to be archived.</summary>
    /// <param name="record">The record.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <returns><see langword="true"/> when the record should move to the archive.</returns>
    public bool IsDue(AuditRecord record, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.RecordedOnUtc + ArchiveAfter <= nowUtc;
    }
}

/// <summary>
/// Everything one user session did, reconstructed from the records that share its session id. A projection —
/// it is derived from the trail on demand and never stored, so it can never drift from the records.
/// </summary>
/// <param name="SessionId">The session.</param>
/// <param name="Tenant">The owning tenant.</param>
/// <param name="Actor">Who the session belonged to.</param>
/// <param name="StartedOnUtc">When the first recorded action happened.</param>
/// <param name="EndedOnUtc">When the last recorded action happened.</param>
/// <param name="RecordCount">How many records the session produced.</param>
public sealed record AuditSession(
    string SessionId,
    string Tenant,
    AuditActor Actor,
    DateTimeOffset StartedOnUtc,
    DateTimeOffset EndedOnUtc,
    int RecordCount)
{
    /// <summary>Gets how long the session lasted.</summary>
    public TimeSpan Duration => EndedOnUtc - StartedOnUtc;
}

/// <summary>The verdict of verifying a stretch of the audit chain.</summary>
/// <param name="IsValid">Whether every record verified.</param>
/// <param name="Verified">How many records were checked.</param>
/// <param name="BrokenAtSequence">The sequence number of the first bad record, when one was found.</param>
/// <param name="Reason">What was wrong with it.</param>
public sealed record AuditChainVerification(
    bool IsValid, int Verified, long? BrokenAtSequence = null, string? Reason = null)
{
    /// <summary>Creates a verdict for a chain that verified cleanly.</summary>
    /// <param name="verified">How many records were checked.</param>
    /// <returns>The verdict.</returns>
    public static AuditChainVerification Valid(int verified) => new(true, verified);

    /// <summary>Creates a verdict for a chain that failed verification.</summary>
    /// <param name="verified">How many records were checked before the failure.</param>
    /// <param name="sequence">The sequence number of the bad record.</param>
    /// <param name="reason">What was wrong.</param>
    /// <returns>The verdict.</returns>
    public static AuditChainVerification Broken(int verified, long sequence, string reason) =>
        new(false, verified, sequence, reason);
}
