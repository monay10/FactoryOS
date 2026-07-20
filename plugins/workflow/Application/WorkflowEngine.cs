using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Workflow.Domain;

namespace FactoryOS.Plugins.Workflow.Application;

/// <summary>
/// The heart of the Workflow module: it matches a normalized <see cref="WorkflowSignal"/> against the configured
/// rule set and, on a match, requests the rule's action with <see cref="WorkflowActionRequested"/>. It is the one
/// place actions are emitted, so every trigger handler funnels through it. Delivery being at-least-once, a signal
/// is acted on once (deduplicated by the triggering event's id).
/// </summary>
public sealed class WorkflowEngine
{
    private readonly IEventBus _bus;
    private readonly IWorkflowRuleSet _ruleSet;
    private readonly IProcessedEventLog _processed;

    /// <summary>Initializes a new instance of the <see cref="WorkflowEngine"/> class.</summary>
    /// <param name="bus">The event bus to publish actions on.</param>
    /// <param name="ruleSet">The configured rule set.</param>
    /// <param name="processed">The processed-event log for idempotency.</param>
    public WorkflowEngine(IEventBus bus, IWorkflowRuleSet ruleSet, IProcessedEventLog processed)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(ruleSet);
        ArgumentNullException.ThrowIfNull(processed);
        _bus = bus;
        _ruleSet = ruleSet;
        _processed = processed;
    }

    /// <summary>Processes a normalized signal: matches a rule and requests its action, exactly once.</summary>
    /// <param name="signal">The normalized trigger.</param>
    /// <param name="cancellationToken">A token to cancel the publish.</param>
    public async Task ProcessAsync(WorkflowSignal signal, CancellationToken cancellationToken)
    {
        var rule = _ruleSet.Resolve(signal.TriggerType);
        if (rule is null)
        {
            return; // no rule configured for this trigger
        }

        // At-least-once delivery: request the action once per triggering event.
        if (!_processed.TryMarkProcessed(signal.SourceEventId))
        {
            return;
        }

        await _bus.PublishAsync(
            new WorkflowActionRequested
            {
                Tenant = signal.Tenant,
                TriggerType = signal.TriggerType,
                Subject = signal.Subject,
                Action = rule.Action,
                Priority = rule.Priority,
                Channel = rule.Channel,
                OccurredAt = signal.OccurredAt,
                SourceEventId = signal.SourceEventId,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
