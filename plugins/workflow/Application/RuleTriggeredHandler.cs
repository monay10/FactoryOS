using System.Globalization;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Workflow.Domain;

namespace FactoryOS.Plugins.Workflow.Application;

/// <summary>
/// Normalizes a <see cref="RuleTriggered"/> — a fired Rule Engine automation — into a workflow signal and processes
/// it. This is what gives a rule action a destination: the Rule Engine emits the normalized fact that a threshold
/// was crossed, and the tenant's workflow rule for the <c>RuleTriggered</c> trigger routes it to a notification
/// channel. The specifics of the fired rule (its id, metric, observed value and its own requested action) travel in
/// the subject, so a channel such as the energy desk sees exactly what fired without either module referencing the
/// other.
/// </summary>
public sealed class RuleTriggeredHandler : IEventHandler<RuleTriggered>
{
    private readonly WorkflowEngine _engine;

    /// <summary>Initializes a new instance of the <see cref="RuleTriggeredHandler"/> class.</summary>
    /// <param name="engine">The workflow engine.</param>
    public RuleTriggeredHandler(WorkflowEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
    }

    /// <inheritdoc />
    public Task HandleAsync(RuleTriggered integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var subject = string.Format(
            CultureInfo.InvariantCulture,
            "Rule '{0}' matched on {1}: {2} {3} {4} → {5}",
            integrationEvent.RuleId,
            integrationEvent.MeterId,
            integrationEvent.Metric,
            integrationEvent.Value,
            integrationEvent.Operator,
            integrationEvent.Action);

        return _engine.ProcessAsync(
            new WorkflowSignal(
                integrationEvent.Tenant,
                nameof(RuleTriggered),
                subject,
                integrationEvent.TriggeredAt,
                integrationEvent.EventId),
            cancellationToken);
    }
}
