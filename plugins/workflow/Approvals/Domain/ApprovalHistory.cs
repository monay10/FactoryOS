namespace FactoryOS.Plugins.Workflow.Approvals.Domain;

/// <summary>A single audit entry recording something that happened to an approval.</summary>
/// <param name="ApprovalId">The approval the entry belongs to.</param>
/// <param name="OccurredOnUtc">When the entry was recorded.</param>
/// <param name="Action">A short action name (e.g. <c>started</c>, <c>approved</c>, <c>stage-advanced</c>).</param>
/// <param name="Actor">Who caused it, if known.</param>
/// <param name="Detail">Optional additional detail.</param>
public sealed record ApprovalHistoryEntry(
    Guid ApprovalId, DateTimeOffset OccurredOnUtc, string Action, string? Actor, string? Detail);

/// <summary>An append-only audit trail of what happened to an approval, in order.</summary>
public sealed class ApprovalHistory
{
    private readonly List<ApprovalHistoryEntry> _entries = [];

    /// <summary>Gets the recorded entries in order.</summary>
    public IReadOnlyList<ApprovalHistoryEntry> Entries => _entries;

    /// <summary>Appends an entry.</summary>
    /// <param name="entry">The entry to append.</param>
    public void Append(ApprovalHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries.Add(entry);
    }
}

/// <summary>Grants a principal a set of actions on an approval.</summary>
/// <param name="Kind">How to interpret the principal.</param>
/// <param name="Principal">The user id, role or group name.</param>
/// <param name="Permission">The actions granted.</param>
public sealed record ApprovalPermissionGrant(
    ApprovalPrincipalKind Kind, string Principal, ApprovalPermission Permission);
