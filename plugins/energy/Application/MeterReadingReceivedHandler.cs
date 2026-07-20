using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Energy.Domain;

namespace FactoryOS.Plugins.Energy.Application;

/// <summary>
/// The Energy module's consumer of <see cref="MeterReadingReceived"/>. For each reading it records
/// consumption and, once enough history exists, detects spikes against the rolling baseline — publishing its
/// own events back onto the bus. It never references another module. Delivery is at-least-once, so the handler
/// deduplicates by event id before mutating any baseline, keeping the consumer idempotent.
/// </summary>
public sealed class MeterReadingReceivedHandler : IEventHandler<MeterReadingReceived>
{
    private readonly IEventBus _bus;
    private readonly IEnergyBaselineStore _baselines;
    private readonly IEnergyReadModel _readModel;
    private readonly IProcessedEventLog _processed;
    private readonly EnergyOptions _options;

    /// <summary>Initializes a new instance of the <see cref="MeterReadingReceivedHandler"/> class.</summary>
    /// <param name="bus">The event bus to publish energy events on.</param>
    /// <param name="baselines">The rolling-baseline store.</param>
    /// <param name="readModel">The energy read model the dashboard queries.</param>
    /// <param name="processed">The processed-event log for idempotency.</param>
    /// <param name="options">The module options.</param>
    public MeterReadingReceivedHandler(
        IEventBus bus,
        IEnergyBaselineStore baselines,
        IEnergyReadModel readModel,
        IProcessedEventLog processed,
        EnergyOptions options)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(baselines);
        ArgumentNullException.ThrowIfNull(readModel);
        ArgumentNullException.ThrowIfNull(processed);
        ArgumentNullException.ThrowIfNull(options);
        _bus = bus;
        _baselines = baselines;
        _readModel = readModel;
        _processed = processed;
        _options = options;
    }

    /// <inheritdoc />
    public async Task HandleAsync(
        MeterReadingReceived integrationEvent,
        EventContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        // At-least-once delivery: skip a reading already folded into the baseline.
        if (!_processed.TryMarkProcessed(integrationEvent.EventId))
        {
            return;
        }

        var reading = integrationEvent.Reading;
        var key = new EnergyMeterKey(reading.Tenant, reading.MeterId, reading.Metric);
        var baseline = _baselines.Observe(key, reading.Value);

        _readModel.RecordReading(new EnergyMeterReading(
            reading.Tenant,
            reading.MeterId,
            reading.Metric,
            reading.Value,
            baseline.PriorAverage,
            reading.Unit,
            reading.ReadingAt));

        await _bus.PublishAsync(
            new EnergyConsumptionRecorded
            {
                Tenant = reading.Tenant,
                MeterId = reading.MeterId,
                Metric = reading.Metric,
                Value = reading.Value,
                Unit = reading.Unit,
                ReadingAt = reading.ReadingAt,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var evaluation = EnergySpikeEvaluator.Evaluate(reading.Value, baseline, _options);
        if (!evaluation.IsSpike)
        {
            return;
        }

        _readModel.RecordSpike(new EnergySpikeEntry(
            reading.Tenant,
            reading.MeterId,
            reading.Metric,
            reading.Value,
            evaluation.Baseline,
            evaluation.DeltaPercent,
            reading.Unit,
            reading.ReadingAt));

        await _bus.PublishAsync(
            new EnergySpikeDetected
            {
                Tenant = reading.Tenant,
                MeterId = reading.MeterId,
                Metric = reading.Metric,
                Value = reading.Value,
                Baseline = evaluation.Baseline,
                DeltaPercent = evaluation.DeltaPercent,
                Unit = reading.Unit,
                ReadingAt = reading.ReadingAt,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
