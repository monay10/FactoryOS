using System.Globalization;
using FactoryOS.Contracts.Events;

namespace FactoryOS.Agents.Insight.Application;

/// <summary>Normalizes a <see cref="SafetyStandDownTriggered"/> into an insight signal and runs the agent on it.</summary>
public sealed class SafetyStandDownTriggeredHandler : IEventHandler<SafetyStandDownTriggered>
{
    private readonly InsightEngine _engine;

    /// <summary>Initializes a new instance of the <see cref="SafetyStandDownTriggeredHandler"/> class.</summary>
    /// <param name="engine">The insight engine.</param>
    public SafetyStandDownTriggeredHandler(InsightEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
    }

    /// <inheritdoc />
    public Task HandleAsync(SafetyStandDownTriggered integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var subject = string.Format(
            CultureInfo.InvariantCulture,
            "Safety stand-down at {0} ({1}); trigger severity {2}, {3} incidents in window",
            integrationEvent.SiteId,
            integrationEvent.Reason,
            integrationEvent.TriggerSeverity,
            integrationEvent.WindowIncidentCount);

        return _engine.GenerateAsync(
            new InsightSignal(
                integrationEvent.Tenant,
                nameof(SafetyStandDownTriggered),
                subject,
                integrationEvent.OccurredAt,
                integrationEvent.EventId),
            cancellationToken);
    }
}
