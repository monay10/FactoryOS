namespace FactoryOS.Plugins.Workflow.Tasks.Domain;

/// <summary>A single audit entry recording something that happened to a human task.</summary>
/// <param name="TaskId">The task the entry belongs to.</param>
/// <param name="OccurredOnUtc">When the entry was recorded.</param>
/// <param name="Action">A short action name (e.g. <c>created</c>, <c>assigned</c>, <c>completed</c>).</param>
/// <param name="Actor">Who caused it, if known.</param>
/// <param name="Detail">Optional additional detail.</param>
public sealed record HumanTaskHistoryEntry(
    Guid TaskId, DateTimeOffset OccurredOnUtc, string Action, string? Actor, string? Detail);

/// <summary>An append-only audit trail of what happened to a human task, in order.</summary>
public sealed class HumanTaskHistory
{
    private readonly List<HumanTaskHistoryEntry> _entries = [];

    /// <summary>Gets the recorded entries in order.</summary>
    public IReadOnlyList<HumanTaskHistoryEntry> Entries => _entries;

    /// <summary>Appends an entry.</summary>
    /// <param name="entry">The entry to append.</param>
    public void Append(HumanTaskHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries.Add(entry);
    }
}
