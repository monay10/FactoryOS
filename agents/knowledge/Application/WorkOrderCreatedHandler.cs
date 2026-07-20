using System.Globalization;
using FactoryOS.Contracts.Events;

namespace FactoryOS.Agents.Knowledge.Application;

/// <summary>Narrates a <see cref="WorkOrderCreated"/> into a knowledge document and ingests it, so the Company
/// Brain can later answer questions about raised work orders. References the shared event only.</summary>
public sealed class WorkOrderCreatedHandler : IEventHandler<WorkOrderCreated>
{
    private readonly KnowledgeIngestor _ingestor;

    /// <summary>Initializes a new instance of the <see cref="WorkOrderCreatedHandler"/> class.</summary>
    /// <param name="ingestor">The knowledge ingestor.</param>
    public WorkOrderCreatedHandler(KnowledgeIngestor ingestor)
    {
        ArgumentNullException.ThrowIfNull(ingestor);
        _ingestor = ingestor;
    }

    /// <inheritdoc />
    public Task HandleAsync(WorkOrderCreated integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var workOrder = integrationEvent.WorkOrder;
        var text = string.Format(
            CultureInfo.InvariantCulture,
            "On {0:u}, work order {1} was raised for asset {2} at tenant {3}: {4}. Reason: {5}. Status: {6}.",
            integrationEvent.OccurredOnUtc,
            workOrder.Number,
            workOrder.AssetCode,
            workOrder.Tenant,
            workOrder.Title,
            integrationEvent.Reason,
            workOrder.Status);

        return _ingestor.IngestAsync(
            new KnowledgeSignal(
                workOrder.Tenant,
                $"activity/workorder/{integrationEvent.EventId:N}",
                text,
                integrationEvent.EventId),
            cancellationToken);
    }
}
