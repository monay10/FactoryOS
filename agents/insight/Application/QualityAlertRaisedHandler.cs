using System.Globalization;
using FactoryOS.Contracts.Events;

namespace FactoryOS.Agents.Insight.Application;

/// <summary>Normalizes a <see cref="QualityAlertRaised"/> into an insight signal and runs the agent on it.</summary>
public sealed class QualityAlertRaisedHandler : IEventHandler<QualityAlertRaised>
{
    private readonly InsightEngine _engine;

    /// <summary>Initializes a new instance of the <see cref="QualityAlertRaisedHandler"/> class.</summary>
    /// <param name="engine">The insight engine.</param>
    public QualityAlertRaisedHandler(InsightEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engine = engine;
    }

    /// <inheritdoc />
    public Task HandleAsync(QualityAlertRaised integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var subject = string.Format(
            CultureInfo.InvariantCulture,
            "Defect rate {0:P1} on {1}/{2} breached threshold {3:P1} over {4} inspected units",
            integrationEvent.DefectRate,
            integrationEvent.LineId,
            integrationEvent.ProductId,
            integrationEvent.Threshold,
            integrationEvent.WindowInspectedUnits);

        return _engine.GenerateAsync(
            new InsightSignal(
                integrationEvent.Tenant,
                nameof(QualityAlertRaised),
                subject,
                integrationEvent.InspectedAt,
                integrationEvent.EventId),
            cancellationToken);
    }
}
