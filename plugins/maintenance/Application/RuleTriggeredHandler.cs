using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Maintenance.Domain;

namespace FactoryOS.Plugins.Maintenance.Application;

/// <summary>
/// The Maintenance module's consumer of <see cref="RuleTriggered"/>. When a fired rule requests one of the
/// module's configured maintenance actions, it raises a corrective work order and announces it with
/// <see cref="WorkOrderCreated"/>. It references no other module — only the shared event vocabulary — so the Rule
/// Engine that emitted the trigger and the Maintenance module that acts on it stay fully decoupled. Because the
/// work-order number is derived from the trigger's event id and the store adds by number, redelivery of the same
/// trigger neither creates a second order nor re-publishes.
/// </summary>
public sealed class RuleTriggeredHandler : IEventHandler<RuleTriggered>
{
    private readonly IEventBus _bus;
    private readonly IWorkOrderStore _store;
    private readonly MaintenanceOptions _options;

    /// <summary>Initializes a new instance of the <see cref="RuleTriggeredHandler"/> class.</summary>
    /// <param name="bus">The event bus to publish work-order events on.</param>
    /// <param name="store">The work-order store.</param>
    /// <param name="options">The module options carrying the actions this module reacts to.</param>
    public RuleTriggeredHandler(IEventBus bus, IWorkOrderStore store, MaintenanceOptions options)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(options);
        _bus = bus;
        _store = store;
        _options = options;
    }

    /// <inheritdoc />
    public async Task HandleAsync(RuleTriggered integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        if (!_options.RuleActions.Contains(integrationEvent.Action, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        var workOrder = RuleWorkOrderFactory.FromTrigger(integrationEvent, _options);

        // Idempotent: the number is deterministic per trigger, so a duplicate add is a no-op and we do not re-announce.
        if (!_store.TryAdd(workOrder))
        {
            return;
        }

        await _bus.PublishAsync(
            new WorkOrderCreated
            {
                WorkOrder = workOrder,
                Reason = $"Rule:{integrationEvent.RuleId}",
                SourceEventId = integrationEvent.EventId,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
