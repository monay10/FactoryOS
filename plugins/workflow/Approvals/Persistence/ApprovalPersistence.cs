using System.Collections.Concurrent;
using FactoryOS.Plugins.Workflow.Approvals.Domain;

namespace FactoryOS.Plugins.Workflow.Approvals.Persistence;

/// <summary>The registry of approval definitions, keyed by definition key.</summary>
public interface IApprovalRepository
{
    /// <summary>Registers a definition (idempotent by key; last registration wins).</summary>
    /// <param name="definition">The definition to register.</param>
    void Register(ApprovalDefinition definition);

    /// <summary>Gets a definition by key.</summary>
    /// <param name="key">The definition key.</param>
    /// <returns>The definition, or <see langword="null"/> when not registered.</returns>
    ApprovalDefinition? Get(string key);

    /// <summary>Gets every registered definition.</summary>
    /// <returns>The definitions.</returns>
    IReadOnlyCollection<ApprovalDefinition> All();
}

/// <summary>An in-memory <see cref="IApprovalRepository"/>.</summary>
public sealed class InMemoryApprovalRepository : IApprovalRepository
{
    private readonly ConcurrentDictionary<string, ApprovalDefinition> _definitions = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void Register(ApprovalDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _definitions[definition.Key] = definition;
    }

    /// <inheritdoc />
    public ApprovalDefinition? Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _definitions.TryGetValue(key, out var definition) ? definition : null;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<ApprovalDefinition> All() => _definitions.Values.ToArray();
}

/// <summary>The persistence store for approval instances.</summary>
public interface IApprovalStore
{
    /// <summary>Saves an approval (insert or update by id).</summary>
    /// <param name="approval">The approval to save.</param>
    void Save(ApprovalInstance approval);

    /// <summary>Gets an approval by id.</summary>
    /// <param name="id">The approval id.</param>
    /// <returns>The approval, or <see langword="null"/> when not found.</returns>
    ApprovalInstance? Get(Guid id);

    /// <summary>Lists the approvals that have a pending step assigned to a principal.</summary>
    /// <param name="assignee">The assignee.</param>
    /// <returns>The approvals awaiting the assignee.</returns>
    IReadOnlyCollection<ApprovalInstance> ListByAssignee(string assignee);

    /// <summary>Lists the approvals in a given status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The matching approvals.</returns>
    IReadOnlyCollection<ApprovalInstance> ListByStatus(ApprovalStatus status);

    /// <summary>Lists every approval that has not reached a terminal status.</summary>
    /// <returns>The open approvals.</returns>
    IReadOnlyCollection<ApprovalInstance> ListOpen();
}

/// <summary>An in-memory <see cref="IApprovalStore"/>. Approvals are held by reference, so saves are updates.</summary>
public sealed class InMemoryApprovalStore : IApprovalStore
{
    private readonly ConcurrentDictionary<Guid, ApprovalInstance> _approvals = new();

    /// <inheritdoc />
    public void Save(ApprovalInstance approval)
    {
        ArgumentNullException.ThrowIfNull(approval);
        _approvals[approval.Id] = approval;
    }

    /// <inheritdoc />
    public ApprovalInstance? Get(Guid id) => _approvals.TryGetValue(id, out var approval) ? approval : null;

    /// <inheritdoc />
    public IReadOnlyCollection<ApprovalInstance> ListByAssignee(string assignee)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assignee);
        return _approvals.Values
            .Where(approval => approval.ActiveStageSteps.Any(step =>
                step.Status == ApprovalParticipantStatus.Pending
                && string.Equals(step.Assignee, assignee, StringComparison.Ordinal)))
            .ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyCollection<ApprovalInstance> ListByStatus(ApprovalStatus status) =>
        _approvals.Values.Where(approval => approval.Status == status).ToArray();

    /// <inheritdoc />
    public IReadOnlyCollection<ApprovalInstance> ListOpen() =>
        _approvals.Values.Where(approval => !approval.IsFinished).ToArray();
}

/// <summary>The persistence store for approval history entries, kept queryable independently of the approval.</summary>
public interface IApprovalHistoryRepository
{
    /// <summary>Appends a history entry.</summary>
    /// <param name="entry">The entry.</param>
    void Append(ApprovalHistoryEntry entry);

    /// <summary>Lists the history entries for an approval, oldest first.</summary>
    /// <param name="approvalId">The approval id.</param>
    /// <returns>The entries.</returns>
    IReadOnlyList<ApprovalHistoryEntry> ByApproval(Guid approvalId);
}

/// <summary>An in-memory <see cref="IApprovalHistoryRepository"/>.</summary>
public sealed class InMemoryApprovalHistoryRepository : IApprovalHistoryRepository
{
    private readonly ConcurrentDictionary<Guid, List<ApprovalHistoryEntry>> _byApproval = new();

    /// <inheritdoc />
    public void Append(ApprovalHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var list = _byApproval.GetOrAdd(entry.ApprovalId, _ => []);
        lock (list)
        {
            list.Add(entry);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ApprovalHistoryEntry> ByApproval(Guid approvalId)
    {
        if (!_byApproval.TryGetValue(approvalId, out var list))
        {
            return [];
        }

        lock (list)
        {
            return list.OrderBy(entry => entry.OccurredOnUtc).ToArray();
        }
    }
}
