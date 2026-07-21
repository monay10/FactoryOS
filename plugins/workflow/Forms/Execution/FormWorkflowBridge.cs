using FactoryOS.Plugins.Workflow.Engine.Execution;

namespace FactoryOS.Plugins.Forms.Engine.Execution;

/// <summary>
/// The seam between the forms engine and the stateful workflow engine. When a form bound to a workflow
/// activity is submitted, the runtime asks the bridge to complete that activity so the workflow advances. This
/// is the only coupling between the two runtimes; the workflow runtime itself is never modified.
/// </summary>
public interface IFormWorkflowBridge
{
    /// <summary>Completes the workflow activity a submitted form satisfied.</summary>
    /// <param name="workflowInstanceId">The workflow instance id.</param>
    /// <param name="activityNodeId">The workflow activity node id.</param>
    /// <param name="outcome">The submitted values passed to the workflow as the activity outcome.</param>
    /// <param name="cancellationToken">A token to cancel the resumption.</param>
    /// <returns>A task that completes when the workflow has advanced.</returns>
    Task CompleteActivityAsync(
        Guid workflowInstanceId,
        string activityNodeId,
        IReadOnlyDictionary<string, object?> outcome,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The default <see cref="IFormWorkflowBridge"/>, which resumes the workflow through the existing stateful
/// <see cref="WorkflowEngine"/> façade. It calls the engine's public completion API only — the workflow
/// runtime is used, never changed.
/// </summary>
public sealed class WorkflowFormBridge : IFormWorkflowBridge
{
    private readonly WorkflowEngine _workflowEngine;

    /// <summary>Initializes a new instance of the <see cref="WorkflowFormBridge"/> class.</summary>
    /// <param name="workflowEngine">The stateful workflow engine to resume.</param>
    public WorkflowFormBridge(WorkflowEngine workflowEngine)
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
}
