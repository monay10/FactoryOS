using System.Globalization;
using FactoryOS.Contracts.Events;

namespace FactoryOS.Agents.Knowledge.Application;

/// <summary>Narrates a <see cref="LowStockDetected"/> into a knowledge document and ingests it, so the Company Brain
/// can later answer questions about stockouts ("which items dropped below their reorder point this month?").
/// References the shared event only, never the Warehouse module.</summary>
public sealed class LowStockDetectedHandler : IEventHandler<LowStockDetected>
{
    private readonly KnowledgeIngestor _ingestor;

    /// <summary>Initializes a new instance of the <see cref="LowStockDetectedHandler"/> class.</summary>
    /// <param name="ingestor">The knowledge ingestor.</param>
    public LowStockDetectedHandler(KnowledgeIngestor ingestor)
    {
        ArgumentNullException.ThrowIfNull(ingestor);
        _ingestor = ingestor;
    }

    /// <inheritdoc />
    public Task HandleAsync(LowStockDetected integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var text = string.Format(
            CultureInfo.InvariantCulture,
            "On {0:u}, low stock was detected at tenant {1}: item {2} in warehouse {3} fell to {4:0.##} on hand, at or below its reorder point of {5:0.##}.",
            integrationEvent.OccurredAt,
            integrationEvent.Tenant,
            integrationEvent.Sku,
            integrationEvent.WarehouseId,
            integrationEvent.OnHand,
            integrationEvent.ReorderPoint);

        return _ingestor.IngestAsync(
            new KnowledgeSignal(
                integrationEvent.Tenant,
                $"activity/warehouse/{integrationEvent.EventId:N}",
                text,
                integrationEvent.EventId),
            cancellationToken);
    }
}
