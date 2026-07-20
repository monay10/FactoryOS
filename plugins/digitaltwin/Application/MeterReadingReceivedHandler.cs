using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.DigitalTwin.Domain;

namespace FactoryOS.Plugins.DigitalTwin.Application;

/// <summary>
/// Mirrors a <see cref="MeterReadingReceived"/> onto its asset's twin as the latest value of the reading's
/// metric. The asset is the reading's meter. Deduplicates by event id; the registry additionally ignores
/// out-of-order readings. References the shared telemetry event, never the producing bridge or module.
/// </summary>
public sealed class MeterReadingReceivedHandler : IEventHandler<MeterReadingReceived>
{
    private readonly IAssetTwinRegistry _registry;
    private readonly IProcessedEventLog _processed;

    /// <summary>Initializes a new instance of the <see cref="MeterReadingReceivedHandler"/> class.</summary>
    /// <param name="registry">The asset twin registry.</param>
    /// <param name="processed">The processed-event log for idempotency.</param>
    public MeterReadingReceivedHandler(IAssetTwinRegistry registry, IProcessedEventLog processed)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(processed);
        _registry = registry;
        _processed = processed;
    }

    /// <inheritdoc />
    public Task HandleAsync(MeterReadingReceived integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        if (_processed.TryMarkProcessed(integrationEvent.EventId))
        {
            var reading = integrationEvent.Reading;
            _registry.RecordMetric(
                reading.Tenant,
                reading.MeterId,
                new MetricReading(reading.Metric, reading.Value, reading.Unit, reading.ReadingAt));
        }

        return Task.CompletedTask;
    }
}
