using FactoryOS.Plugins.Workflow.Engine.Execution;

namespace FactoryOS.Plugins.Workflow.Tasks.Execution;

/// <summary>
/// The seam between the human task engine and the stateful workflow engine. Completing (or rejecting) a
/// workflow-bound task advances its activity; cancelling one cancels the owning workflow instance. This is the
/// only coupling to the workflow runtime, and it uses the engine's public API only — the workflow runtime
/// itself is never modified.
/// </summary>
public interface IHumanTaskWorkflowBridge
{
    /// <summary>Completes the workflow activity a task satisfied, passing the decision outcome.</summary>
    /// <param name="workflowInstanceId">The workflow instance id.</param>
    /// <param name="activityNodeId">The workflow activity node id.</param>
    /// <param name="outcome">The values passed to the workflow as the activity outcome.</param>
    /// <param name="cancellationToken">A token to cancel the resumption.</param>
    /// <returns>A task that completes when the workflow has advanced.</returns>
    Task CompleteActivityAsync(
        Guid workflowInstanceId,
        string activityNodeId,
        IReadOnlyDictionary<string, object?> outcome,
        CancellationToken cancellationToken = default);

    /// <summary>Cancels the workflow instance that owns a cancelled task's activity.</summary>
    /// <param name="workflowInstanceId">The workflow instance id.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the workflow instance has been cancelled.</returns>
    Task CancelActivityAsync(Guid workflowInstanceId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The default <see cref="IHumanTaskWorkflowBridge"/>, resuming or cancelling the workflow through the existing
/// stateful <see cref="WorkflowEngine"/> façade.
/// </summary>
public sealed class HumanTaskWorkflowBridge : IHumanTaskWorkflowBridge
{
    private readonly WorkflowEngine _workflowEngine;

    /// <summary>Initializes a new instance of the <see cref="HumanTaskWorkflowBridge"/> class.</summary>
    /// <param name="workflowEngine">The stateful workflow engine.</param>
    public HumanTaskWorkflowBridge(WorkflowEngine workflowEngine)
    {
        ArgumentNullException.ThrowIfNull(workflowEngine);
        _workflowEngine = workflowEngine;
    }

    /// <inheritdoc />
    public Task CompleteActivityAsync(
        Guid workflowInstanceId,
        string activityNodeId,
        IReadOnlyDictionary<string, object?> outcome,
        CancellationToken cancellationToken = default) =>
        _workflowEngine.CompleteActivityAsync(workflowInstanceId, activityNodeId, outcome, cancellationToken);

    /// <inheritdoc />
    public Task CancelActivityAsync(Guid workflowInstanceId, CancellationToken cancellationToken = default) =>
        _workflowEngine.CancelAsync(workflowInstanceId, cancellationToken);
}
