using FactoryOS.Plugins.Workflow.Audit.Configuration;
using FactoryOS.Plugins.Workflow.Audit.Domain;

namespace FactoryOS.Plugins.Workflow.Audit.Execution;

/// <summary>
/// Decides whether an entry is worth recording at all, before it reaches the chain. Filtering happens at the
/// door rather than at read time on purpose: a record that was never sealed costs nothing and, more
/// importantly, cannot later be deleted to hide something — the chain only ever contains what was admitted.
/// </summary>
public sealed class AuditFilter
{
    private readonly AuditEngineOptions _options;

    /// <summary>Initializes a new instance of the <see cref="AuditFilter"/> class.</summary>
    /// <param name="options">The engine options.</param>
    public AuditFilter(AuditEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <summary>Gets a value indicating whether an entry should be recorded.</summary>
    /// <param name="entry">The entry.</param>
    /// <returns><see langword="true"/> when the entry passes the filter.</returns>
    public bool ShouldRecord(AuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.Severity < _options.MinimumSeverity)
        {
            return false;
        }

        return !_options.ExcludedCategories.Contains(entry.Category);
    }
}

/// <summary>
/// Resolves which retention and archive policies apply to a record. A policy naming a category beats a
/// catch-all, so a tenant can keep security records for seven years while letting routine workflow chatter age
/// out in ninety days without writing two separate rule sets.
/// </summary>
public sealed class AuditPolicyEvaluator
{
    private readonly Persistence.IAuditRepository _policies;

    /// <summary>Initializes a new instance of the <see cref="AuditPolicyEvaluator"/> class.</summary>
    /// <param name="policies">The policy repository.</param>
    public AuditPolicyEvaluator(Persistence.IAuditRepository policies)
    {
        ArgumentNullException.ThrowIfNull(policies);
        _policies = policies;
    }

    /// <summary>Resolves the archive policy that applies to a record.</summary>
    /// <param name="record">The record.</param>
    /// <returns>The most specific matching policy, or <see langword="null"/> when none applies.</returns>
    public AuditArchivePolicy? ArchivePolicyFor(AuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return _policies.ArchivePolicies()
            .Where(policy => policy.Covers(record))
            .OrderByDescending(policy => policy.Category is not null)
            .FirstOrDefault();
    }

    /// <summary>Resolves the retention policy that applies to a record.</summary>
    /// <param name="record">The record.</param>
    /// <returns>The most specific matching policy, or <see langword="null"/> when none applies.</returns>
    public AuditRetentionPolicy? RetentionPolicyFor(AuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return _policies.RetentionPolicies()
            .Where(policy => policy.Covers(record))
            .OrderByDescending(policy => policy.Category is not null)
            .FirstOrDefault();
    }

    /// <summary>Gets a value indicating whether a record is due to move into the archive.</summary>
    /// <param name="record">The record.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <returns><see langword="true"/> when an archive policy says it is due.</returns>
    public bool IsDueForArchive(AuditRecord record, DateTimeOffset nowUtc) =>
        ArchivePolicyFor(record) is { } policy && policy.IsDue(record, nowUtc);

    /// <summary>Gets the retention policy a record has outlived, when it has.</summary>
    /// <param name="record">The record.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <returns>The expired policy, or <see langword="null"/> when the record is still within retention.</returns>
    public AuditRetentionPolicy? ExpiredPolicyFor(AuditRecord record, DateTimeOffset nowUtc) =>
        RetentionPolicyFor(record) is { } policy && policy.HasExpired(record, nowUtc) ? policy : null;
}
