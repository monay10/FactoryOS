using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugins.Workflow.Tasks.Diagnostics;
using FactoryOS.Plugins.Workflow.Tasks.Domain;
using FactoryOS.Plugins.Workflow.Tasks.Events;
using FactoryOS.Plugins.Workflow.Tasks.Persistence;

namespace FactoryOS.Plugins.Workflow.Tasks.Execution;

/// <summary>
/// Cancels a human task. When the task is bound to a workflow activity, cancelling it cancels the owning
/// workflow instance through the bridge (the workflow's public cancel API) — the workflow runtime itself is
/// never modified.
/// </summary>
public sealed class TaskCancellationService
{
    private readonly IHumanTaskStore _store;
    private readonly IHumanTaskHistoryRepository _history;
    private readonly IHumanTaskEventSink _events;
    private readonly HumanTaskExecutor _executor;
    private readonly HumanTaskMetrics _metrics;
    private readonly IDateTimeProvider _clock;
    private readonly IHumanTaskWorkflowBridge? _workflowBridge;

    /// <summary>Initializes a new instance of the <see cref="TaskCancellationService"/> class.</summary>
    /// <param name="store">The task store.</param>
    /// <param name="history">The history repository.</param>
    /// <param name="events">The event sink.</param>
    /// <param name="executor">The task executor.</param>
    /// <param name="metrics">The engine metrics.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="workflowBridge">The workflow bridge, when the workflow engine is available.</param>
    public TaskCancellationService(
        IHumanTaskStore store,
        IHumanTaskHistoryRepository history,
        IHumanTaskEventSink events,
        HumanTaskExecutor executor,
        HumanTaskMetrics metrics,
        IDateTimeProvider clock,
        IHumanTaskWorkflowBridge? workflowBridge = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(clock);
        _store = store;
        _history = history;
        _events = events;
        _executor = executor;
        _metrics = metrics;
        _clock = clock;
        _workflowBridge = workflowBridge;
    }

    /// <summary>Cancels a task, optionally cancelling its owning workflow instance.</summary>
    /// <param name="taskId">The task id.</param>
    /// <param name="actor">Who is cancelling it.</param>
    /// <param name="reason">An optional reason.</param>
    /// <param name="cancelWorkflow">
    /// Whether to cancel the owning workflow instance for a workflow-bound task. When <see langword="false"/>
    /// only the task is cancelled and the workflow stays paused on its activity.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The cancelled task, or <see langword="null"/> when unknown.</returns>
    public async Task<HumanTaskInstance?> CancelAsync(
        Guid taskId,
        string? actor = null,
        string? reason = null,
        bool cancelWorkflow = true,
        CancellationToken cancellationToken = default)
    {
        var instance = _store.Get(taskId);
        if (instance is null)
        {
            return null;
        }

        var now = _clock.UtcNow;
        _history.Append(_executor.Cancel(instance, now, actor, reason));
        _store.Save(instance);
        _metrics.RecordCancelled();
        _events.Publish(new HumanTaskCancelled(instance.Id, instance.Tenant, now, instance.DefinitionKey));

        if (cancelWorkflow && instance.IsWorkflowBound)
        {
            if (_workflowBridge is null)
            {
                throw new InvalidOperationException(
                    $"Task '{instance.Id}' is bound to a workflow activity but no workflow bridge is registered.");
            }

            await _workflowBridge.CancelActivityAsync(instance.WorkflowInstanceId!.Value, cancellationToken)
                .ConfigureAwait(false);
        }

        return instance;
    }
}
