using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Reporting.Domain;

namespace FactoryOS.Plugins.Reporting.Application;

/// <summary>
/// Folds an <see cref="OeeCalculated"/> fact into the daily OEE report, bucketed by the reading's UTC calendar
/// day. Deduplicates by event id so a redelivered reading does not skew the day's average or sample count. It
/// references the shared OEE event, never the OEE module.
/// </summary>
public sealed class OeeCalculatedHandler : IEventHandler<OeeCalculated>
{
    private readonly IOeeReport _report;
    private readonly IProcessedEventLog _processed;

    /// <summary>Initializes a new instance of the <see cref="OeeCalculatedHandler"/> class.</summary>
    /// <param name="report">the OEE reporting read-model.</param>
    /// <param name="processed">The processed-event log for idempotency.</param>
    public OeeCalculatedHandler(IOeeReport report, IProcessedEventLog processed)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(processed);
        _report = report;
        _processed = processed;
    }

    /// <inheritdoc />
    public Task HandleAsync(OeeCalculated integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        if (_processed.TryMarkProcessed(integrationEvent.EventId))
        {
            var day = DateOnly.FromDateTime(integrationEvent.PeriodEnd.UtcDateTime);
            _report.Record(integrationEvent.Tenant, integrationEvent.MachineId, day, integrationEvent.Oee);
        }

        return Task.CompletedTask;
    }
}
