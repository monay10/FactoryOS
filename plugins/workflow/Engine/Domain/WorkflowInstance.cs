namespace FactoryOS.Plugins.Workflow.Engine.Domain;

/// <summary>A pending activity awaiting external completion: the node it sits on and its resolved assignee.</summary>
/// <param name="NodeId">The activity node id.</param>
/// <param name="ActivityKey">The activity type key.</param>
/// <param name="Assignee">The resolved assignee reference, if any.</param>
public sealed record PendingActivity(string NodeId, string ActivityKey, string? Assignee);

/// <summary>
/// A running (or finished) workflow instance: the live state of one execution of a definition. It carries
/// the active node tokens (more than one while parallel branches run), the variables, the pending activities
/// and timers it waits on, and its execution history. The executor and runtime mutate it through the methods
/// here; there is no code path that reads or writes across tenants — the tenant is fixed at creation.
/// </summary>
public sealed class WorkflowInstance
{
    private readonly List<string> _activeTokens = [];
    private readonly Dictionary<string, PendingActivity> _pendingActivities = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _pendingTimers = new(StringComparer.Ordinal);

    private WorkflowInstance(Guid id, string definitionKey, WorkflowVersion version, string tenant, WorkflowVariables variables)
    {
        Id = id;
        DefinitionKey = definitionKey;
        Version = version;
        Tenant = tenant;
        Variables = variables;
        Status = WorkflowStatus.NotStarted;
        History = new WorkflowHistory();
    }

    /// <summary>Gets the instance identifier.</summary>
    public Guid Id { get; }

    /// <summary>Gets the definition key this instance runs.</summary>
    public string DefinitionKey { get; }

    /// <summary>Gets the definition version this instance runs.</summary>
    public WorkflowVersion Version { get; }

    /// <summary>Gets the owning tenant.</summary>
    public string Tenant { get; }

    /// <summary>Gets the instance variables.</summary>
    public WorkflowVariables Variables { get; }

    /// <summary>Gets the current status.</summary>
    public WorkflowStatus Status { get; private set; }

    /// <summary>Gets the failure reason when <see cref="Status"/> is <see cref="WorkflowStatus.Failed"/>.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>Gets the execution history.</summary>
    public WorkflowHistory History { get; }

    /// <summary>Gets the node ids that currently hold a token (repeats while parallel branches run).</summary>
    public IReadOnlyList<string> ActiveTokens => _activeTokens;

    /// <summary>Gets the pending activities awaiting external completion, keyed by node id.</summary>
    public IReadOnlyDictionary<string, PendingActivity> PendingActivities => _pendingActivities;

    /// <summary>Gets the pending timers and their due instants, keyed by node id.</summary>
    public IReadOnlyDictionary<string, DateTimeOffset> PendingTimers => _pendingTimers;

    /// <summary>Gets a value indicating whether the instance has reached a terminal status.</summary>
    public bool IsFinished =>
        Status is WorkflowStatus.Completed or WorkflowStatus.Cancelled or WorkflowStatus.Failed;

    /// <summary>Creates a new instance for a definition, seeded with variables.</summary>
    /// <param name="id">The instance id.</param>
    /// <param name="definitionKey">The definition key.</param>
    /// <param name="version">The definition version.</param>
    /// <param name="tenant">The owning tenant.</param>
    /// <param name="variables">The seed variables.</param>
    /// <returns>The new instance.</returns>
    public static WorkflowInstance Create(
        Guid id, string definitionKey, WorkflowVersion version, string tenant, WorkflowVariables? variables = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return new WorkflowInstance(id, definitionKey, version, tenant, variables ?? new WorkflowVariables());
    }

    /// <summary>Marks the instance as running.</summary>
    public void MarkRunning() => Status = WorkflowStatus.Running;

    /// <summary>Marks the instance as completed.</summary>
    public void MarkCompleted() => Status = WorkflowStatus.Completed;

    /// <summary>Marks the instance as cancelled.</summary>
    public void MarkCancelled() => Status = WorkflowStatus.Cancelled;

    /// <summary>Marks the instance as failed and records the reason.</summary>
    /// <param name="reason">The failure reason.</param>
    public void MarkFailed(string reason)
    {
        FailureReason = reason;
        Status = WorkflowStatus.Failed;
    }

    /// <summary>Adds a token on a node.</summary>
    /// <param name="nodeId">The node id.</param>
    public void AddToken(string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        _activeTokens.Add(nodeId);
    }

    /// <summary>Removes a single token from a node.</summary>
    /// <param name="nodeId">The node id.</param>
    /// <returns><see langword="true"/> when a token was present and removed.</returns>
    public bool RemoveToken(string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        return _activeTokens.Remove(nodeId);
    }

    /// <summary>Counts the tokens currently on a node.</summary>
    /// <param name="nodeId">The node id.</param>
    /// <returns>The token count.</returns>
    public int TokensOn(string nodeId) => _activeTokens.Count(id => string.Equals(id, nodeId, StringComparison.Ordinal));

    /// <summary>Records a pending activity.</summary>
    /// <param name="activity">The pending activity.</param>
    public void AddPendingActivity(PendingActivity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);
        _pendingActivities[activity.NodeId] = activity;
    }

    /// <summary>Removes a pending activity.</summary>
    /// <param name="nodeId">The activity node id.</param>
    public void RemovePendingActivity(string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        _pendingActivities.Remove(nodeId);
    }

    /// <summary>Records a pending timer due instant.</summary>
    /// <param name="nodeId">The timer node id.</param>
    /// <param name="dueUtc">When the timer is due.</param>
    public void AddPendingTimer(string nodeId, DateTimeOffset dueUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        _pendingTimers[nodeId] = dueUtc;
    }

    /// <summary>Removes a pending timer.</summary>
    /// <param name="nodeId">The timer node id.</param>
    public void RemovePendingTimer(string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId);
        _pendingTimers.Remove(nodeId);
    }
}
