using System.Globalization;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Workflow.Domain;

namespace FactoryOS.Plugins.Workflow.Application;

/// <summary>Normalizes a <see cref="QualityAlertRaised"/> into a workflow signal and processes it.</summary>
public sealed class QualityAlertRaisedHandler : IEventHandler<QualityAlertRaised>
{
    private readonly WorkflowEngine _engine;

    /// <summary>Initializes a new instance of the <see cref="QualityAlertRaisedHandler"/> class.</summary>
    /// <param name="engine">The workflow engine.</param>
    public QualityAlertRaisedHandler(WorkflowEngine engine)
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
            "Quality defect rate {0:P1} on {1}/{2}",
            integrationEvent.DefectRate,
            integrationEvent.LineId,
            integrationEvent.ProductId);

        return _engine.ProcessAsync(
            new WorkflowSignal(
                integrationEvent.Tenant,
                nameof(QualityAlertRaised),
                subject,
                integrationEvent.InspectedAt,
                integrationEvent.EventId),
            cancellationToken);
    }
}
