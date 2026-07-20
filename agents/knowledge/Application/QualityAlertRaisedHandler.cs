using System.Globalization;
using FactoryOS.Contracts.Events;

namespace FactoryOS.Agents.Knowledge.Application;

/// <summary>Narrates a <see cref="QualityAlertRaised"/> into a knowledge document and ingests it, so the Company
/// Brain can later answer questions about quality alerts. References the shared event only.</summary>
public sealed class QualityAlertRaisedHandler : IEventHandler<QualityAlertRaised>
{
    private readonly KnowledgeIngestor _ingestor;

    /// <summary>Initializes a new instance of the <see cref="QualityAlertRaisedHandler"/> class.</summary>
    /// <param name="ingestor">The knowledge ingestor.</param>
    public QualityAlertRaisedHandler(KnowledgeIngestor ingestor)
    {
        ArgumentNullException.ThrowIfNull(ingestor);
        _ingestor = ingestor;
    }

    /// <inheritdoc />
    public Task HandleAsync(QualityAlertRaised integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var text = string.Format(
            CultureInfo.InvariantCulture,
            "On {0:u}, a quality alert was raised on line {1} for product {2} at tenant {3}: rolling defect rate {4:P1} exceeded threshold {5:P1} over {6} units ({7} defective).",
            integrationEvent.InspectedAt,
            integrationEvent.LineId,
            integrationEvent.ProductId,
            integrationEvent.Tenant,
            integrationEvent.DefectRate,
            integrationEvent.Threshold,
            integrationEvent.WindowInspectedUnits,
            integrationEvent.WindowDefectiveUnits);

        return _ingestor.IngestAsync(
            new KnowledgeSignal(
                integrationEvent.Tenant,
                $"activity/quality/{integrationEvent.EventId:N}",
                text,
                integrationEvent.EventId),
            cancellationToken);
    }
}
