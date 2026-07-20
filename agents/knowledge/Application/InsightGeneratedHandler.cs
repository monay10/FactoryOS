using System.Globalization;
using FactoryOS.Contracts.Events;

namespace FactoryOS.Agents.Knowledge.Application;

/// <summary>Narrates an <see cref="InsightGenerated"/> into a knowledge document and ingests it, so the Company Brain
/// remembers its own past root-cause hypotheses and can ground later answers on them ("has this failure been
/// explained before?"). References the shared event only, never the Insight agent.</summary>
public sealed class InsightGeneratedHandler : IEventHandler<InsightGenerated>
{
    private readonly KnowledgeIngestor _ingestor;

    /// <summary>Initializes a new instance of the <see cref="InsightGeneratedHandler"/> class.</summary>
    /// <param name="ingestor">The knowledge ingestor.</param>
    public InsightGeneratedHandler(KnowledgeIngestor ingestor)
    {
        ArgumentNullException.ThrowIfNull(ingestor);
        _ingestor = ingestor;
    }

    /// <inheritdoc />
    public Task HandleAsync(InsightGenerated integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var text = string.Format(
            CultureInfo.InvariantCulture,
            "On {0:u}, the AI Insight agent produced an insight at tenant {1} for a {2} on {3}: {4} (model {5}).",
            integrationEvent.GeneratedAt,
            integrationEvent.Tenant,
            integrationEvent.TriggerType,
            integrationEvent.Subject,
            integrationEvent.Insight,
            integrationEvent.Model);

        return _ingestor.IngestAsync(
            new KnowledgeSignal(
                integrationEvent.Tenant,
                $"activity/insight/{integrationEvent.EventId:N}",
                text,
                integrationEvent.EventId),
            cancellationToken);
    }
}
