using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugins.Workflow.Approvals.Diagnostics;
using FactoryOS.Plugins.Workflow.Approvals.Domain;
using FactoryOS.Plugins.Workflow.Approvals.Events;
using FactoryOS.Plugins.Workflow.Approvals.Persistence;

namespace FactoryOS.Plugins.Workflow.Approvals.Execution;

/// <summary>
/// Records participant votes and drives an approval forward. After each vote the active stage is evaluated
/// against its policy: a rejection finishes the approval (routing a bound workflow down its rejection branch);
/// an approval advances to the next stage in a sequential approval, or finishes the whole approval when it was
/// the last stage; an undecided stage waits for more votes.
/// </summary>
public sealed class ApprovalDecisionService
{
    private readonly IApprovalStore _store;
    private readonly IApprovalRepository _repository;
    private readonly IApprovalHistoryRepository _history;
    private readonly IApprovalEventSink _events;
    private readonly ApprovalExecutor _executor;
    private readonly ApprovalPolicyEvaluator _policyEvaluator;
    private readonly ApprovalRuntime _runtime;
    private readonly ApprovalCompletionService _completion;
    private readonly ApprovalMetrics _metrics;
    private readonly IDateTimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="ApprovalDecisionService"/> class.</summary>
    /// <param name="store">The approval store.</param>
    /// <param name="repository">The definition repository.</param>
    /// <param name="history">The history repository.</param>
    /// <param name="events">The event sink.</param>
    /// <param name="executor">The approval executor.</param>
    /// <param name="policyEvaluator">The policy evaluator.</param>
    /// <param name="runtime">The runtime (used to activate the next stage).</param>
    /// <param name="completion">The completion service.</param>
    /// <param name="metrics">The engine metrics.</param>
    /// <param name="clock">The clock.</param>
    public ApprovalDecisionService(
        IApprovalStore store,
        IApprovalRepository repository,
        IApprovalHistoryRepository history,
        IApprovalEventSink events,
        ApprovalExecutor executor,
        ApprovalPolicyEvaluator policyEvaluator,
        ApprovalRuntime runtime,
        ApprovalCompletionService completion,
        ApprovalMetrics metrics,
        IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(policyEvaluator);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(completion);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(clock);
        _store = store;
        _repository = repository;
        _history = history;
        _events = events;
        _executor = executor;
        _policyEvaluator = policyEvaluator;
        _runtime = runtime;
        _completion = completion;
        _metrics = metrics;
        _clock = clock;
    }

    /// <summary>Records a participant's vote and advances the approval.</summary>
    /// <param name="approvalId">The approval id.</param>
    /// <param name="participantId">The participant casting the vote.</param>
    /// <param name="kind">Whether the participant approves or rejects.</param>
    /// <param name="decidedBy">The concrete user casting the vote.</param>
    /// <param name="comment">An optional comment.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The updated approval, or <see langword="null"/> when unknown.</returns>
    public async Task<ApprovalInstance?> DecideAsync(
        Guid approvalId,
        string participantId,
        ApprovalDecisionKind kind,
        string? decidedBy = null,
        string? comment = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(participantId);
        var instance = _store.Get(approvalId);
        if (instance is null)
        {
            return null;
        }

        var definition = _repository.Get(instance.DefinitionKey)
            ?? throw new InvalidOperationException($"Approval definition '{instance.DefinitionKey}' is not registered.");

        var now = _clock.UtcNow;
        var decision = new ApprovalDecision(participantId, kind, decidedBy, comment, now);
        _history.Append(_executor.Vote(instance, decision, now));
        _metrics.RecordVote();
        _events.Publish(kind == ApprovalDecisionKind.Approve
            ? new ApprovalApproved(instance.Id, instance.Tenant, now, instance.DefinitionKey, participantId, decidedBy)
            : new ApprovalRejected(instance.Id, instance.Tenant, now, instance.DefinitionKey, participantId, decidedBy));

        var stage = definition.Stages[instance.CurrentLevel.Value - 1];
        var outcome = _policyEvaluator.Evaluate(stage.Policy, instance.ActiveStageSteps);

        switch (outcome)
        {
            case ApprovalOutcome.Rejected:
                await _completion.FinishAsync(instance, ApprovalOutcome.Rejected, cancellationToken).ConfigureAwait(false);
                break;
            case ApprovalOutcome.Approved when instance.CurrentLevel.Value < definition.Stages.Count:
                _runtime.ActivateStage(definition, instance, definition.Stages[instance.CurrentLevel.Value], now);
                _store.Save(instance);
                break;
            case ApprovalOutcome.Approved:
                await _completion.FinishAsync(instance, ApprovalOutcome.Approved, cancellationToken).ConfigureAwait(false);
                break;
            default:
                _store.Save(instance);
                break;
        }

        return instance;
    }
}
