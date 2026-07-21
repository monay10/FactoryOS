namespace FactoryOS.Plugins.Workflow.Engine.Domain;

/// <summary>A single entry in an instance's execution history.</summary>
/// <param name="Sequence">The monotonically increasing sequence number.</param>
/// <param name="TimestampUtc">When the entry was recorded.</param>
/// <param name="NodeId">The node the entry concerns.</param>
/// <param name="State">The node state at the time.</param>
/// <param name="Detail">An optional human-readable detail.</param>
public sealed record WorkflowHistoryEntry(
    int Sequence, DateTimeOffset TimestampUtc, string NodeId, WorkflowState State, string? Detail);

/// <summary>The ordered execution history of a workflow instance — an append-only audit of node transitions.</summary>
public sealed class WorkflowHistory
{
    private readonly List<WorkflowHistoryEntry> _entries = [];

    /// <summary>Gets the recorded entries in order.</summary>
    public IReadOnlyList<WorkflowHistoryEntry> Entries => _entries;

    /// <summary>Appends an entry.</summary>
    /// <param name="timestampUtc">When the entry occurred.</param>
    /// <param name="nodeId">The node the entry concerns.</param>
    /// <param name="state">The node state.</param>
    /// <param name="detail">An optional detail.</param>
    /// <returns>The appended entry.</returns>
    public WorkflowHistoryEntry Append(DateTimeOffset timestampUtc, string nodeId, WorkflowState state, string? detail = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        var entry = new WorkflowHistoryEntry(_entries.Count + 1, timestampUtc, nodeId, state, detail);
        _entries.Add(entry);
        return entry;
    }
}
