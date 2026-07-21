using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugins.Workflow.Tasks.Diagnostics;
using FactoryOS.Plugins.Workflow.Tasks.Domain;
using FactoryOS.Plugins.Workflow.Tasks.Events;
using FactoryOS.Plugins.Workflow.Tasks.Persistence;

namespace FactoryOS.Plugins.Workflow.Tasks.Execution;

/// <summary>
/// Completes or rejects a human task with a decision. Both transitions advance a workflow-bound task's
/// activity — completion and rejection are outcomes the workflow can branch on — passing the decision
/// variables plus a <c>taskOutcome</c>/<c>approved</c> marker as the activity outcome.
/// </summary>
public sealed class TaskCompletionService
{
    /// <summary>The outcome key carrying the decision name (Approved/Rejected/Done).</summary>
    public const string OutcomeKey = "taskOutcome";

    /// <summary>The outcome key carrying whether the decision was an approval.</summary>
    public const string ApprovedKey = "approved";

    private readonly IHumanTaskStore _store;
    private readonly IHumanTaskHistoryRepository _history;
    private readonly IHumanTaskEventSink _events;
    private readonly HumanTaskExecutor _executor;
    private readonly HumanTaskMetrics _metrics;
    private readonly IDateTimeProvider _clock;
    private readonly IHumanTaskWorkflowBridge? _workflowBridge;

    /// <summary>Initializes a new instance of the <see cref="TaskCompletionService"/> class.</summary>
    /// <param name="store">The task store.</param>
    /// <param name="history">The history repository.</param>
    /// <param name="events">The event sink.</param>
    /// <param name="executor">The task executor.</param>
    /// <param name="metrics">The engine metrics.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="workflowBridge">The workflow bridge, when the workflow engine is available.</param>
    public TaskCompletionService(
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

    /// <summary>Completes a task.</summary>
    /// <param name="taskId">The task id.</param>
    /// <param name="decision">The decision.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The completed task, or <see langword="null"/> when unknown.</returns>
    public async Task<HumanTaskInstance?> CompleteAsync(
        Guid taskId, HumanTaskDecision decision, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var instance = _store.Get(taskId);
        if (instance is null)
        {
            return null;
        }

        var now = _clock.UtcNow;
        _history.Append(_executor.Complete(instance, decision, now));
        _store.Save(instance);
        _metrics.RecordCompleted();
        _events.Publish(new HumanTaskCompleted(
            instance.Id, instance.Tenant, now, instance.DefinitionKey, decision.DecidedBy));

        await AdvanceWorkflowAsync(instance, decision, cancellationToken).ConfigureAwait(false);
        return instance;
    }

    /// <summary>Rejects a task.</summary>
    /// <param name="taskId">The task id.</param>
    /// <param name="decision">The decision.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The rejected task, or <see langword="null"/> when unknown.</returns>
    public async Task<HumanTaskInstance?> RejectAsync(
        Guid taskId, HumanTaskDecision decision, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var instance = _store.Get(taskId);
        if (instance is null)
        {
            return null;
        }

        var now = _clock.UtcNow;
        _history.Append(_executor.Reject(instance, decision, now));
        _store.Save(instance);
        _metrics.RecordRejected();
        _events.Publish(new HumanTaskRejected(
            instance.Id, instance.Tenant, now, instance.DefinitionKey, decision.DecidedBy));

        await AdvanceWorkflowAsync(instance, decision, cancellationToken).ConfigureAwait(false);
        return instance;
    }

    private async Task AdvanceWorkflowAsync(
        HumanTaskInstance instance, HumanTaskDecision decision, CancellationToken cancellationToken)
    {
        if (!instance.IsWorkflowBound)
        {
            return;
        }

        if (_workflowBridge is null)
        {
            throw new InvalidOperationException(
                $"Task '{instance.Id}' is bound to a workflow activity but no workflow bridge is registered.");
        }

        var outcome = new Dictionary<string, object?>(decision.Variables, StringComparer.Ordinal)
        {
            [OutcomeKey] = decision.Outcome.ToString(),
            [ApprovedKey] = decision.Outcome == HumanTaskOutcome.Approved,
        };
        await _workflowBridge.CompleteActivityAsync(
            instance.WorkflowInstanceId!.Value, instance.WorkflowActivityNodeId!, outcome, cancellationToken)
            .ConfigureAwait(false);
    }
}
