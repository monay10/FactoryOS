using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugins.Workflow.Approvals.Diagnostics;
using FactoryOS.Plugins.Workflow.Approvals.Domain;
using FactoryOS.Plugins.Workflow.Approvals.Events;
using FactoryOS.Plugins.Workflow.Approvals.Persistence;

namespace FactoryOS.Plugins.Workflow.Approvals.Execution;

/// <summary>
/// Cancels an approval. When the approval is bound to a workflow activity, cancelling it cancels the owning
/// workflow instance through the bridge (the workflow's public cancel API) — the workflow runtime itself is
/// never modified.
/// </summary>
public sealed class ApprovalCancellationService
{
    private readonly IApprovalStore _store;
    private readonly IApprovalHistoryRepository _history;
    private readonly IApprovalEventSink _events;
    private readonly ApprovalExecutor _executor;
    private readonly ApprovalMetrics _metrics;
    private readonly IDateTimeProvider _clock;
    private readonly IApprovalWorkflowBridge? _workflowBridge;

    /// <summary>Initializes a new instance of the <see cref="ApprovalCancellationService"/> class.</summary>
    /// <param name="store">The approval store.</param>
    /// <param name="history">The history repository.</param>
    /// <param name="events">The event sink.</param>
    /// <param name="executor">The approval executor.</param>
    /// <param name="metrics">The engine metrics.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="workflowBridge">The workflow bridge, when the workflow engine is available.</param>
    public ApprovalCancellationService(
        IApprovalStore store,
        IApprovalHistoryRepository history,
        IApprovalEventSink events,
        ApprovalExecutor executor,
        ApprovalMetrics metrics,
        IDateTimeProvider clock,
        IApprovalWorkflowBridge? workflowBridge = null)
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

    /// <summary>Cancels an approval, optionally cancelling its owning workflow instance.</summary>
    /// <param name="approvalId">The approval id.</param>
    /// <param name="actor">Who is cancelling it.</param>
    /// <param name="reason">An optional reason.</param>
    /// <param name="cancelWorkflow">Whether to cancel the owning workflow instance for a bound approval.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The cancelled approval, or <see langword="null"/> when unknown.</returns>
    public async Task<ApprovalInstance?> CancelAsync(
        Guid approvalId,
        string? actor = null,
        string? reason = null,
        bool cancelWorkflow = true,
        CancellationToken cancellationToken = default)
    {
        var instance = _store.Get(approvalId);
        if (instance is null)
        {
            return null;
        }

        var now = _clock.UtcNow;
        _history.Append(_executor.Cancel(instance, now, actor, reason));
        _store.Save(instance);
        _metrics.RecordCancelled();
        _events.Publish(new ApprovalCancelled(instance.Id, instance.Tenant, now, instance.DefinitionKey));

        if (cancelWorkflow && instance.IsWorkflowBound)
        {
            if (_workflowBridge is null)
            {
                throw new InvalidOperationException(
                    $"Approval '{instance.Id}' is bound to a workflow activity but no workflow bridge is registered.");
            }

            await _workflowBridge.CancelActivityAsync(instance.WorkflowInstanceId!.Value, cancellationToken)
                .ConfigureAwait(false);
        }

        return instance;
    }
}
