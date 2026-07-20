using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Carbon.Domain;

namespace FactoryOS.Plugins.Carbon.Application;

/// <summary>
/// The Carbon module's consumer of <see cref="EnergyConsumptionRecorded"/>. It converts each energy reading into
/// a carbon-equivalent emission using the configured factor, accrues it into the source's running total and
/// announces it with <see cref="CarbonEmissionCalculated"/>. It references no other module — the energy fact
/// comes from Energy purely over the bus. Because the total is accumulated, delivery being at-least-once, the
/// handler deduplicates by event id before accruing. Metrics with no positive emission factor are ignored.
/// </summary>
public sealed class EnergyConsumptionRecordedHandler : IEventHandler<EnergyConsumptionRecorded>
{
    private readonly IEventBus _bus;
    private readonly ICarbonLedger _ledger;
    private readonly IProcessedEventLog _processed;
    private readonly CarbonOptions _options;

    /// <summary>Initializes a new instance of the <see cref="EnergyConsumptionRecordedHandler"/> class.</summary>
    /// <param name="bus">The event bus to publish carbon events on.</param>
    /// <param name="ledger">The cumulative-emission ledger.</param>
    /// <param name="processed">The processed-event log for idempotency.</param>
    /// <param name="options">The module options.</param>
    public EnergyConsumptionRecordedHandler(
        IEventBus bus,
        ICarbonLedger ledger,
        IProcessedEventLog processed,
        CarbonOptions options)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(processed);
        ArgumentNullException.ThrowIfNull(options);
        _bus = bus;
        _ledger = ledger;
        _processed = processed;
        _options = options;
    }

    /// <inheritdoc />
    public async Task HandleAsync(
        EnergyConsumptionRecorded integrationEvent,
        EventContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        // At-least-once delivery: skip a reading already accrued into the total.
        if (!_processed.TryMarkProcessed(integrationEvent.EventId))
        {
            return;
        }

        var factor = EmissionFactorResolver.Resolve(integrationEvent.Metric, _options);
        if (factor <= 0m)
        {
            return; // no emission factor for this metric → nothing to report
        }

        var co2eKg = CarbonCalculator.Co2eKg(integrationEvent.Value, factor);
        var cumulative = _ledger.Accrue(
            new CarbonSourceKey(integrationEvent.Tenant, integrationEvent.MeterId),
            co2eKg);

        await _bus.PublishAsync(
            new CarbonEmissionCalculated
            {
                Tenant = integrationEvent.Tenant,
                Source = integrationEvent.MeterId,
                Metric = integrationEvent.Metric,
                EnergyValue = integrationEvent.Value,
                EnergyUnit = integrationEvent.Unit,
                EmissionFactor = factor,
                Co2eKg = co2eKg,
                CumulativeCo2eKg = cumulative,
                OccurredAt = integrationEvent.ReadingAt,
                SourceEventId = integrationEvent.EventId,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
