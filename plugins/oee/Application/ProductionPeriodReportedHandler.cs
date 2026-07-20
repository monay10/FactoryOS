using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Oee.Domain;

namespace FactoryOS.Plugins.Oee.Application;

/// <summary>
/// The OEE module's consumer of <see cref="ProductionPeriodReported"/>. It computes the OEE factors, stores the
/// snapshot and announces it with <see cref="OeeCalculated"/>. It references no other module — only the shared
/// event vocabulary. Storage is keyed by machine and period, so a redelivered period is neither recomputed nor
/// re-announced; a period with no planned time is skipped as un-computable.
/// </summary>
public sealed class ProductionPeriodReportedHandler : IEventHandler<ProductionPeriodReported>
{
    private readonly IEventBus _bus;
    private readonly IOeeStore _store;
    private readonly OeeOptions _options;

    /// <summary>Initializes a new instance of the <see cref="ProductionPeriodReportedHandler"/> class.</summary>
    /// <param name="bus">The event bus to publish OEE events on.</param>
    /// <param name="store">The OEE snapshot store.</param>
    /// <param name="options">The module options.</param>
    public ProductionPeriodReportedHandler(IEventBus bus, IOeeStore store, OeeOptions options)
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
        ProductionPeriodReported integrationEvent,
        EventContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        if (integrationEvent.PlannedTimeSeconds <= 0m)
        {
            return; // no planned time → OEE is undefined for this period
        }

        var score = OeeCalculator.Calculate(
            integrationEvent.PlannedTimeSeconds,
            integrationEvent.RunTimeSeconds,
            integrationEvent.IdealCycleTimeSeconds,
            integrationEvent.TotalCount,
            integrationEvent.GoodCount);

        var snapshot = new OeeSnapshot(
            integrationEvent.Tenant,
            integrationEvent.MachineId,
            integrationEvent.PeriodStart,
            integrationEvent.PeriodEnd,
            score);

        if (!_store.TryAdd(snapshot))
        {
            return; // this machine-period was already calculated (idempotent)
        }

        await _bus.PublishAsync(
            new OeeCalculated
            {
                Tenant = integrationEvent.Tenant,
                MachineId = integrationEvent.MachineId,
                PeriodStart = integrationEvent.PeriodStart,
                PeriodEnd = integrationEvent.PeriodEnd,
                Availability = score.Availability,
                Performance = score.Performance,
                Quality = score.Quality,
                Oee = score.Oee,
                MeetsTarget = score.Oee >= _options.TargetOee,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
