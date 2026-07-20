using System.Text;
using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.Storage;
using FactoryOS.Plugins.Reporting.Domain;

namespace FactoryOS.Plugins.Reporting.Application;

/// <summary>
/// The Reporting module's consumer of <see cref="ScheduledTaskDue"/>. When a due schedule requests the report
/// action, it renders the tenant's current OEE read-model to a CSV artifact, stores it through the object store,
/// and announces <see cref="ReportGenerated"/>. It references the shared events, the pure renderer and the object
/// store contract only — never the Scheduler, the object-store implementation, or any consumer of the report.
/// </summary>
/// <remarks>
/// The object key is stable per schedule, so a re-run overwrites rather than piling up. Storage happens before
/// the event is marked processed, so a storage failure throws and the bus retries; once stored and announced,
/// redelivery re-stores the identical artifact but announces only once.
/// </remarks>
public sealed class ScheduledTaskDueHandler : IEventHandler<ScheduledTaskDue>
{
    private readonly IEventBus _bus;
    private readonly IObjectStore _store;
    private readonly IOeeReport _report;
    private readonly IProcessedEventLog _processed;
    private readonly ReportingOptions _options;

    /// <summary>Initializes a new instance of the <see cref="ScheduledTaskDueHandler"/> class.</summary>
    /// <param name="bus">The event bus to announce generated reports on.</param>
    /// <param name="store">The object store the artifact is written to.</param>
    /// <param name="report">The OEE read-model rendered into the artifact.</param>
    /// <param name="processed">The processed-event log for idempotency.</param>
    /// <param name="options">The module options.</param>
    public ScheduledTaskDueHandler(
        IEventBus bus,
        IObjectStore store,
        IOeeReport report,
        IProcessedEventLog processed,
        ReportingOptions options)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(processed);
        ArgumentNullException.ThrowIfNull(options);
        _bus = bus;
        _store = store;
        _report = report;
        _processed = processed;
        _options = options;
    }

    /// <inheritdoc />
    public async Task HandleAsync(ScheduledTaskDue integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        if (!string.Equals(integrationEvent.Action, _options.ReportAction, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var (csv, rowCount) = OeeCsvRenderer.Render(_report, integrationEvent.Tenant);
        var bytes = Encoding.UTF8.GetBytes(csv);
        var key = $"{_options.ReportKeyPrefix}{integrationEvent.ScheduleId}.csv";

        await _store.PutAsync(
            new StoredObject
            {
                Tenant = integrationEvent.Tenant,
                Key = key,
                ContentType = "text/csv",
                Content = bytes,
            },
            cancellationToken).ConfigureAwait(false);

        // Announce once: a redelivery re-stores the identical artifact but must not re-announce.
        if (!_processed.TryMarkProcessed(integrationEvent.EventId))
        {
            return;
        }

        await _bus.PublishAsync(
            new ReportGenerated
            {
                Tenant = integrationEvent.Tenant,
                ReportId = integrationEvent.ScheduleId,
                ObjectKey = key,
                ContentType = "text/csv",
                SizeBytes = bytes.Length,
                RowCount = rowCount,
                GeneratedAt = integrationEvent.DueAt,
                SourceEventId = integrationEvent.EventId,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
