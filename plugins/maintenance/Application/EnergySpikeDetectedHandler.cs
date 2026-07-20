using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Maintenance.Domain;

namespace FactoryOS.Plugins.Maintenance.Application;

/// <summary>
/// The Maintenance module's consumer of <see cref="EnergySpikeDetected"/>. It raises a corrective work order
/// for the spiking meter and announces it with <see cref="WorkOrderCreated"/>. It references no other module —
/// only the shared event vocabulary. Because the work-order number is derived from the spike's event id and the
/// store adds by number, redelivery of the same spike neither creates a second order nor re-publishes.
/// </summary>
public sealed class EnergySpikeDetectedHandler : IEventHandler<EnergySpikeDetected>
{
    private readonly IEventBus _bus;
    private readonly IWorkOrderStore _store;
    private readonly MaintenanceOptions _options;

    /// <summary>Initializes a new instance of the <see cref="EnergySpikeDetectedHandler"/> class.</summary>
    /// <param name="bus">The event bus to publish work-order events on.</param>
    /// <param name="store">The work-order store.</param>
    /// <param name="options">The module options.</param>
    public EnergySpikeDetectedHandler(IEventBus bus, IWorkOrderStore store, MaintenanceOptions options)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(options);
        _bus = bus;
        _store = store;
        _options = options;
    }

    /// <inheritdoc />
    public async Task HandleAsync(
        EnergySpikeDetected integrationEvent,
        EventContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var workOrder = SpikeWorkOrderFactory.FromSpike(integrationEvent, _options);

        // Idempotent: the number is deterministic per spike, so a duplicate add is a no-op and we do not re-announce.
        if (!_store.TryAdd(workOrder))
        {
            return;
        }

        await _bus.PublishAsync(
            new WorkOrderCreated
            {
                WorkOrder = workOrder,
                Reason = "EnergySpike",
                SourceEventId = integrationEvent.EventId,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
