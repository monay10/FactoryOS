using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugins.Workflow.Approvals.Diagnostics;
using FactoryOS.Plugins.Workflow.Approvals.Domain;
using FactoryOS.Plugins.Workflow.Approvals.Events;
using FactoryOS.Plugins.Workflow.Approvals.Persistence;

namespace FactoryOS.Plugins.Workflow.Approvals.Execution;

/// <summary>
/// Finalizes an approval: applies the terminal outcome, records it, publishes <see cref="ApprovalCompleted"/>
/// (or <see cref="ApprovalExpired"/>), and — when the approval was created from a workflow activity — completes
/// that activity with an <c>approved</c> outcome the workflow branches on. A rejection or a timeout routes down
/// the workflow's rejection branch; an approval routes down its approval branch.
/// </summary>
public sealed class ApprovalCompletionService
{
    /// <summary>The outcome key carrying whether the approval was granted (a coarse branch flag).</summary>
    public const string ApprovedKey = "approved";

    /// <summary>
    /// The outcome key carrying the terminal resolution name (Approved/Rejected/Cancelled/Expired) — the typed
    /// distinction downstream branches, SLA reports and KPIs key on, rather than the coarse <see cref="ApprovedKey"/>.
    /// </summary>
    public const string ResolutionKey = "approvalResolution";

    private readonly IApprovalStore _store;
    private readonly IApprovalHistoryRepository _history;
    private readonly IApprovalEventSink _events;
    private readonly ApprovalExecutor _executor;
    private readonly ApprovalMetrics _metrics;
    private readonly IDateTimeProvider _clock;
    private readonly IApprovalWorkflowBridge? _workflowBridge;

    /// <summary>Initializes a new instance of the <see cref="ApprovalCompletionService"/> class.</summary>
    /// <param name="store">The approval store.</param>
    /// <param name="history">The history repository.</param>
    /// <param name="events">The event sink.</param>
    /// <param name="executor">The approval executor.</param>
    /// <param name="metrics">The engine metrics.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="workflowBridge">The workflow bridge, when the workflow engine is available.</param>
    public ApprovalCompletionService(
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

    /// <summary>Finishes an approval with a terminal outcome and advances any bound workflow.</summary>
    /// <param name="instance">The approval.</param>
    /// <param name="outcome">The terminal outcome (approved or rejected).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the approval is finished and the workflow advanced.</returns>
    public async Task FinishAsync(
        ApprovalInstance instance, ApprovalOutcome outcome, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var now = _clock.UtcNow;
        _history.Append(_executor.Finish(instance, outcome, now));
        _store.Save(instance);

        var approved = outcome == ApprovalOutcome.Approved;
        if (approved)
        {
            _metrics.RecordApproved();
        }
        else
        {
            _metrics.RecordRejected();
        }

        _events.Publish(new ApprovalCompleted(
            instance.Id, instance.Tenant, now, instance.DefinitionKey, approved, instance.Resolution));
        await AdvanceWorkflowAsync(instance, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Expires an approval and routes any bound workflow down the rejection branch.</summary>
    /// <param name="instance">The approval.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the approval is expired and the workflow advanced.</returns>
    public async Task ExpireAsync(ApprovalInstance instance, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var now = _clock.UtcNow;
        _history.Append(_executor.Expire(instance, now));
        _store.Save(instance);
        _metrics.RecordExpired();
        _events.Publish(new ApprovalExpired(instance.Id, instance.Tenant, now, instance.DefinitionKey));
        await AdvanceWorkflowAsync(instance, cancellationToken).ConfigureAwait(false);
    }

    private async Task AdvanceWorkflowAsync(ApprovalInstance instance, CancellationToken cancellationToken)
    {
        if (!instance.IsWorkflowBound)
        {
            return;
        }

        if (_workflowBridge is null)
        {
            throw new InvalidOperationException(
                $"Approval '{instance.Id}' is bound to a workflow activity but no workflow bridge is registered.");
        }

        // The workflow branches on the coarse `approved` flag, but the typed resolution travels alongside it so a
        // decision node — or a downstream SLA / KPI report — can tell a rejection from an expiry from a cancellation.
        var outcome = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [ApprovedKey] = instance.Resolution == ApprovalResolution.Approved,
            [ResolutionKey] = instance.Resolution.ToString(),
        };
        await _workflowBridge.CompleteActivityAsync(
            instance.WorkflowInstanceId!.Value, instance.WorkflowActivityNodeId!, outcome, cancellationToken)
            .ConfigureAwait(false);
    }
}
