using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.DigitalTwin.Domain;

namespace FactoryOS.Plugins.DigitalTwin.Application;

/// <summary>
/// Mirrors an <see cref="OeeCalculated"/> onto its asset's twin as the latest health reading. The asset is the
/// machine. Deduplicates by event id; the registry additionally ignores out-of-order readings. References the
/// shared OEE event, never the OEE module.
/// </summary>
public sealed class OeeCalculatedHandler : IEventHandler<OeeCalculated>
{
    private readonly IAssetTwinRegistry _registry;
    private readonly IProcessedEventLog _processed;

    /// <summary>Initializes a new instance of the <see cref="OeeCalculatedHandler"/> class.</summary>
    /// <param name="registry">The asset twin registry.</param>
    /// <param name="processed">The processed-event log for idempotency.</param>
    public OeeCalculatedHandler(IAssetTwinRegistry registry, IProcessedEventLog processed)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(processed);
        _registry = registry;
        _processed = processed;
    }

    /// <inheritdoc />
    public Task HandleAsync(OeeCalculated integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        if (_processed.TryMarkProcessed(integrationEvent.EventId))
        {
            _registry.RecordHealth(
                integrationEvent.Tenant,
                integrationEvent.MachineId,
                new AssetHealth(integrationEvent.Oee, integrationEvent.MeetsTarget, integrationEvent.PeriodEnd));
        }

        return Task.CompletedTask;
    }
}
