using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugins.Workflow.Approvals.Configuration;
using FactoryOS.Plugins.Workflow.Approvals.Diagnostics;
using FactoryOS.Plugins.Workflow.Approvals.Domain;
using FactoryOS.Plugins.Workflow.Approvals.Events;
using FactoryOS.Plugins.Workflow.Approvals.Persistence;

namespace FactoryOS.Plugins.Workflow.Approvals.Execution;

/// <summary>A summary of what an approval due-work pass did.</summary>
/// <param name="RemindersFired">How many reminders fired.</param>
/// <param name="EscalationsApplied">How many escalations were applied.</param>
/// <param name="ApprovalsExpired">How many approvals were expired.</param>
public sealed record ApprovalDueWorkSummary(int RemindersFired, int EscalationsApplied, int ApprovalsExpired);

/// <summary>
/// Coordinates the parts of the approval engine that touch persistence and the event bus around the pure
/// executor: registering definitions, starting approvals (evaluating auto-decision rules, then activating the
/// first stage with resolved participants — optionally bound to a workflow activity), and running the due-work
/// pass that fires reminders, applies escalations and expires overdue approvals.
/// </summary>
public sealed class ApprovalRuntime
{
    private readonly IApprovalRepository _repository;
    private readonly IApprovalStore _store;
    private readonly IApprovalHistoryRepository _history;
    private readonly IApprovalEventSink _events;
    private readonly ApprovalExecutor _executor;
    private readonly ParticipantResolver _participantResolver;
    private readonly ApprovalDeadlineEngine _deadlineEngine;
    private readonly ApprovalReminderEngine _reminderEngine;
    private readonly ApprovalEscalationEngine _escalationEngine;
    private readonly ApprovalCompletionService _completion;
    private readonly ApprovalMetrics _metrics;
    private readonly ApprovalEngineOptions _options;
    private readonly IDateTimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="ApprovalRuntime"/> class.</summary>
    /// <param name="repository">The definition repository.</param>
    /// <param name="store">The approval store.</param>
    /// <param name="history">The history repository.</param>
    /// <param name="events">The event sink.</param>
    /// <param name="executor">The approval executor.</param>
    /// <param name="participantResolver">The participant resolver.</param>
    /// <param name="deadlineEngine">The deadline engine.</param>
    /// <param name="reminderEngine">The reminder engine.</param>
    /// <param name="escalationEngine">The escalation engine.</param>
    /// <param name="completion">The completion service.</param>
    /// <param name="metrics">The engine metrics.</param>
    /// <param name="options">The engine options.</param>
    /// <param name="clock">The clock.</param>
    public ApprovalRuntime(
        IApprovalRepository repository,
        IApprovalStore store,
        IApprovalHistoryRepository history,
        IApprovalEventSink events,
        ApprovalExecutor executor,
        ParticipantResolver participantResolver,
        ApprovalDeadlineEngine deadlineEngine,
        ApprovalReminderEngine reminderEngine,
        ApprovalEscalationEngine escalationEngine,
        ApprovalCompletionService completion,
        ApprovalMetrics metrics,
        ApprovalEngineOptions options,
        IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(participantResolver);
        ArgumentNullException.ThrowIfNull(deadlineEngine);
        ArgumentNullException.ThrowIfNull(reminderEngine);
        ArgumentNullException.ThrowIfNull(escalationEngine);
        ArgumentNullException.ThrowIfNull(completion);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        _repository = repository;
        _store = store;
        _history = history;
        _events = events;
        _executor = executor;
        _participantResolver = participantResolver;
        _deadlineEngine = deadlineEngine;
        _reminderEngine = reminderEngine;
        _escalationEngine = escalationEngine;
        _completion = completion;
        _metrics = metrics;
        _options = options;
        _clock = clock;
    }

    /// <summary>Registers an approval definition.</summary>
    /// <param name="definition">The definition.</param>
    public void Register(ApprovalDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _repository.Register(definition);
    }

    /// <summary>
    /// Starts an approval: auto-decision rules are evaluated first (a match short-circuits to that outcome),
    /// otherwise the first stage is activated with resolved participants. Optionally binds a workflow activity.
    /// </summary>
    /// <param name="definition">The approval definition.</param>
    /// <param name="context">The approval context (tenant, initiator, values).</param>
    /// <param name="workflowInstanceId">The workflow instance id when created from a workflow activity.</param>
    /// <param name="activityNodeId">The workflow activity node id when created from a workflow activity.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The started approval.</returns>
    public async Task<ApprovalInstance> StartAsync(
        ApprovalDefinition definition,
        ApprovalContext context,
        Guid? workflowInstanceId = null,
        string? activityNodeId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(context);

        if (_options.AutoRegisterDefinitions)
        {
            _repository.Register(definition);
        }

        var now = _clock.UtcNow;
        var instance = ApprovalInstance.Create(
            Guid.NewGuid(), definition.Key, context.Tenant, definition.Title, context.Values);
        if (workflowInstanceId is Guid workflowId && activityNodeId is not null)
        {
            instance.BindToWorkflow(workflowId, activityNodeId);
        }

        _deadlineEngine.Schedule(definition, instance, now);
        _history.Append(_executor.Created(instance, now, context.InitiatedBy));
        _metrics.RecordCreated();
        _events.Publish(new ApprovalCreated(instance.Id, instance.Tenant, now, instance.DefinitionKey));

        var rule = definition.Rules.FirstOrDefault(rule => rule.Matches(context.Values));
        if (rule is not null)
        {
            _store.Save(instance);
            await _completion.FinishAsync(instance, rule.Outcome, cancellationToken).ConfigureAwait(false);
            return instance;
        }

        _history.Append(_executor.Start(instance, now));
        _events.Publish(new ApprovalStarted(instance.Id, instance.Tenant, now, instance.DefinitionKey));
        ActivateStage(definition, instance, definition.Stages[0], now);
        _store.Save(instance);
        return instance;
    }

    /// <summary>Activates a stage: resolves its participants and publishes the assignment events.</summary>
    /// <param name="definition">The definition.</param>
    /// <param name="instance">The approval.</param>
    /// <param name="stage">The stage to activate.</param>
    /// <param name="now">The current instant.</param>
    internal void ActivateStage(
        ApprovalDefinition definition, ApprovalInstance instance, ApprovalStage stage, DateTimeOffset now)
    {
        var steps = _participantResolver.Resolve(stage, instance.Values);
        _history.Append(_executor.ActivateStage(instance, stage.Level, steps, now));
        foreach (var step in steps)
        {
            _events.Publish(new ApprovalAssigned(
                instance.Id, instance.Tenant, now, instance.DefinitionKey, step.ParticipantId, step.Assignee));
        }
    }

    /// <summary>Adds a comment to an approval.</summary>
    /// <param name="approvalId">The approval id.</param>
    /// <param name="author">The comment author.</param>
    /// <param name="text">The comment text.</param>
    /// <returns>The added comment, or <see langword="null"/> when the approval is unknown.</returns>
    public ApprovalComment? AddComment(Guid approvalId, string author, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(author);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var instance = _store.Get(approvalId);
        if (instance is null)
        {
            return null;
        }

        var now = _clock.UtcNow;
        var comment = new ApprovalComment(Guid.NewGuid(), author, text, now);
        _history.Append(_executor.Comment(instance, comment, now));
        _store.Save(instance);
        return comment;
    }

    /// <summary>Runs a due-work pass over every open approval: reminders, escalations and expiries.</summary>
    /// <param name="cancellationToken">A token to cancel the pass.</param>
    /// <returns>A summary of what the pass did.</returns>
    public async Task<ApprovalDueWorkSummary> RunDueAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = _clock.UtcNow;
        var reminders = 0;
        var escalations = 0;
        var expirations = 0;

        foreach (var instance in _store.ListOpen().Take(_options.DueWorkBatchSize))
        {
            var changed = false;

            foreach (var _ in _reminderEngine.Fire(instance, now))
            {
                _history.Append(_executor.ReminderSent(instance, now));
                _metrics.RecordReminder();
                _events.Publish(new ApprovalReminderSent(instance.Id, instance.Tenant, now, instance.DefinitionKey));
                reminders++;
                changed = true;
            }

            foreach (var escalation in _escalationEngine.DueEscalations(instance, now))
            {
                var assignee = escalation.To.Resolve(instance.Values);
                instance.MarkEscalationApplied(escalation);
                _history.Append(_executor.Escalate(instance, assignee, now));
                _metrics.RecordEscalated();
                _events.Publish(new ApprovalEscalated(instance.Id, instance.Tenant, now, instance.DefinitionKey, assignee));
                escalations++;
                changed = true;
            }

            if (_options.ExpireOverdueApprovals && _escalationEngine.IsExpired(instance, now))
            {
                await _completion.ExpireAsync(instance, cancellationToken).ConfigureAwait(false);
                expirations++;
                changed = false; // ExpireAsync already saved the instance.
            }

            if (changed)
            {
                _store.Save(instance);
            }
        }

        return new ApprovalDueWorkSummary(reminders, escalations, expirations);
    }
}
