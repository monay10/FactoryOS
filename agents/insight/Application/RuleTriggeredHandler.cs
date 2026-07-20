using System.Globalization;
using FactoryOS.Contracts.Events;

namespace FactoryOS.Agents.Insight.Application;

/// <summary>
/// Normalizes a <see cref="RuleTriggered"/> into an insight signal and runs the agent on it, so an automated
/// threshold breach (including a computed OEE degradation) earns the same root-cause hypothesis a human-facing
/// alert would — the digital worker explains what the Rule Engine only detected. The agent never references the
/// Rule Engine; it reasons over the shared fact alone.
/// </summary>
public sealed class RuleTriggeredHandler : IEventHandler<RuleTriggered>
{
    private readonly InsightEngine _engine;

    /// <summary>Initializes a new instance of the <see cref="RuleTriggeredHandler"/> class.</summary>
    /// <param name="engine">The insight engine.</param>
    public RuleTriggeredHandler(InsightEngine engine)
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
            "Rule '{0}' fired on {1}: {2} of {3} {4} threshold {5} → requested {6}",
            integrationEvent.RuleId,
            integrationEvent.MeterId,
            integrationEvent.Metric,
            integrationEvent.Value,
            integrationEvent.Operator,
            integrationEvent.Threshold,
            integrationEvent.Action);

        return _engine.GenerateAsync(
            new InsightSignal(
                integrationEvent.Tenant,
                nameof(RuleTriggered),
                subject,
                integrationEvent.TriggeredAt,
                integrationEvent.EventId),
            cancellationToken);
    }
}
