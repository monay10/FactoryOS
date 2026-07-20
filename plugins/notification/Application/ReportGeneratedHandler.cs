using System.Globalization;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Notification.Domain;

namespace FactoryOS.Plugins.Notification.Application;

/// <summary>
/// The Notification module's consumer of <see cref="ReportGenerated"/>. When a report artifact is rendered and
/// stored, it routes the configured report channel to a transport, records the dispatch intent in the outbox and
/// announces it with <see cref="NotificationDispatched"/> — closing the Scheduler → Reporting → File Storage
/// pipeline with a delivery, without referencing the Reporting module or the object store. Delivery over a real
/// transport remains a connector's job. Because the outbox is keyed by the report event's id, redelivery of the
/// same report neither records a duplicate nor re-announces.
/// </summary>
public sealed class ReportGeneratedHandler : IEventHandler<ReportGenerated>
{
    private readonly IEventBus _bus;
    private readonly INotificationOutbox _outbox;
    private readonly NotificationOptions _options;

    /// <summary>Initializes a new instance of the <see cref="ReportGeneratedHandler"/> class.</summary>
    /// <param name="bus">The event bus to announce dispatches on.</param>
    /// <param name="outbox">The notification outbox / read model.</param>
    /// <param name="options">The module options carrying the channel routing and report-notification settings.</param>
    public ReportGeneratedHandler(IEventBus bus, INotificationOutbox outbox, NotificationOptions options)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(outbox);
        ArgumentNullException.ThrowIfNull(options);
        _bus = bus;
        _outbox = outbox;
        _options = options;
    }

    /// <inheritdoc />
    public async Task HandleAsync(
        ReportGenerated integrationEvent,
        EventContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var channel = _options.ReportNotificationChannel;
        var transport = TransportResolver.Resolve(channel, _options);
        var subject = string.Format(
            CultureInfo.InvariantCulture,
            "Report '{0}' is ready ({1} rows, {2} bytes) at {3}",
            integrationEvent.ReportId,
            integrationEvent.RowCount,
            integrationEvent.SizeBytes,
            integrationEvent.ObjectKey);

        var record = new NotificationRecord(
            channel,
            transport,
            _options.ReportNotificationPriority,
            subject,
            _options.ReportNotificationAction,
            integrationEvent.GeneratedAt);

        // Idempotent: the outbox is keyed by the report's event id, so a duplicate is a no-op and we do not re-announce.
        if (!_outbox.TryRecord(integrationEvent.Tenant, integrationEvent.EventId, record))
        {
            return;
        }

        await _bus.PublishAsync(
            new NotificationDispatched
            {
                Tenant = integrationEvent.Tenant,
                Channel = channel,
                Transport = transport,
                Priority = _options.ReportNotificationPriority,
                Subject = subject,
                Action = _options.ReportNotificationAction,
                DispatchedAt = integrationEvent.GeneratedAt,
                SourceEventId = integrationEvent.EventId,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
