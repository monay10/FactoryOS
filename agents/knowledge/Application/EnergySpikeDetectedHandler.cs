using System.Globalization;
using FactoryOS.Contracts.Events;

namespace FactoryOS.Agents.Knowledge.Application;

/// <summary>Narrates an <see cref="EnergySpikeDetected"/> into a knowledge document and ingests it, so the Company
/// Brain can later answer questions about energy anomalies ("which meters spiked last week and by how much?").
/// References the shared event only, never the Energy module.</summary>
public sealed class EnergySpikeDetectedHandler : IEventHandler<EnergySpikeDetected>
{
    private readonly KnowledgeIngestor _ingestor;

    /// <summary>Initializes a new instance of the <see cref="EnergySpikeDetectedHandler"/> class.</summary>
    /// <param name="ingestor">The knowledge ingestor.</param>
    public EnergySpikeDetectedHandler(KnowledgeIngestor ingestor)
    {
        ArgumentNullException.ThrowIfNull(ingestor);
        _ingestor = ingestor;
    }

    /// <inheritdoc />
    public Task HandleAsync(EnergySpikeDetected integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var text = string.Format(
            CultureInfo.InvariantCulture,
            "On {0:u}, an energy spike was detected at tenant {1}: meter {2} read {3:0.##}{4} for {5}, {6:0.#}% above its baseline of {7:0.##}{4}.",
            integrationEvent.ReadingAt,
            integrationEvent.Tenant,
            integrationEvent.MeterId,
            integrationEvent.Value,
            integrationEvent.Unit,
            integrationEvent.Metric,
            integrationEvent.DeltaPercent,
            integrationEvent.Baseline);

        return _ingestor.IngestAsync(
            new KnowledgeSignal(
                integrationEvent.Tenant,
                $"activity/energy/{integrationEvent.EventId:N}",
                text,
                integrationEvent.EventId),
            cancellationToken);
    }
}
