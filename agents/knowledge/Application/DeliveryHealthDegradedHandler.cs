using System.Globalization;
using FactoryOS.Contracts.Events;

namespace FactoryOS.Agents.Knowledge.Application;

/// <summary>Narrates a <see cref="DeliveryHealthDegraded"/> into a knowledge document and ingests it, so the
/// Company Brain can later answer questions about failing notification transports ("why didn't alerts go out on
/// the webhook channel?"). References the shared event only, never the Delivery Health module or the connectors.</summary>
public sealed class DeliveryHealthDegradedHandler : IEventHandler<DeliveryHealthDegraded>
{
    private readonly KnowledgeIngestor _ingestor;

    /// <summary>Initializes a new instance of the <see cref="DeliveryHealthDegradedHandler"/> class.</summary>
    /// <param name="ingestor">The knowledge ingestor.</param>
    public DeliveryHealthDegradedHandler(KnowledgeIngestor ingestor)
    {
        ArgumentNullException.ThrowIfNull(ingestor);
        _ingestor = ingestor;
    }

    /// <inheritdoc />
    public Task HandleAsync(DeliveryHealthDegraded integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var text = string.Format(
            CultureInfo.InvariantCulture,
            "On {0:u}, notification delivery degraded at tenant {1}: transport '{2}' hit {3} consecutive failures ({4} of {5} attempts failed). Last detail: {6}.",
            integrationEvent.DetectedAt,
            integrationEvent.Tenant,
            integrationEvent.Transport,
            integrationEvent.ConsecutiveFailures,
            integrationEvent.Failed,
            integrationEvent.Attempts,
            integrationEvent.LastDetail ?? "(none)");

        return _ingestor.IngestAsync(
            new KnowledgeSignal(
                integrationEvent.Tenant,
                $"activity/delivery/{integrationEvent.EventId:N}",
                text,
                integrationEvent.EventId),
            cancellationToken);
    }
}
