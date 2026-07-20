using System.Globalization;
using FactoryOS.Contracts.Events;

namespace FactoryOS.Agents.Knowledge.Application;

/// <summary>Narrates a <see cref="ProductionOrderCompleted"/> into a knowledge document and ingests it, so the
/// Company Brain can later answer questions about finished production orders ("did order PO-42 complete, and how
/// many units?"). References the shared event only, never the Production module.</summary>
public sealed class ProductionOrderCompletedHandler : IEventHandler<ProductionOrderCompleted>
{
    private readonly KnowledgeIngestor _ingestor;

    /// <summary>Initializes a new instance of the <see cref="ProductionOrderCompletedHandler"/> class.</summary>
    /// <param name="ingestor">The knowledge ingestor.</param>
    public ProductionOrderCompletedHandler(KnowledgeIngestor ingestor)
    {
        ArgumentNullException.ThrowIfNull(ingestor);
        _ingestor = ingestor;
    }

    /// <inheritdoc />
    public Task HandleAsync(ProductionOrderCompleted integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var text = string.Format(
            CultureInfo.InvariantCulture,
            "On {0:u}, production order {1} completed at tenant {2}: product {3} reached its target of {4} units with {5} produced.",
            integrationEvent.CompletedAt,
            integrationEvent.OrderId,
            integrationEvent.Tenant,
            integrationEvent.ProductId,
            integrationEvent.TargetQuantity,
            integrationEvent.TotalProduced);

        return _ingestor.IngestAsync(
            new KnowledgeSignal(
                integrationEvent.Tenant,
                $"activity/production/{integrationEvent.EventId:N}",
                text,
                integrationEvent.EventId),
            cancellationToken);
    }
}
