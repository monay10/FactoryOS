using FactoryOS.Contracts.Connectors;
using FactoryOS.Contracts.Events;

namespace FactoryOS.Connectors.Log.Application;

/// <summary>
/// The bus bridge that drives the outbound connector. It consumes <see cref="NotificationDispatched"/>, ignores
/// dispatches on other transports, delivers the ones on its own transport through the <see cref="IOutboundConnector"/>,
/// and announces the outcome with <see cref="NotificationDelivered"/>. Deduplicated by the dispatch's event id so
/// a redelivery does not send twice. This is the reference wiring an outbound connector uses; it references only
/// the shared contracts, never the Notification module.
/// </summary>
public sealed class NotificationDispatchedHandler : IEventHandler<NotificationDispatched>
{
    private readonly IEventBus _bus;
    private readonly IOutboundConnector _connector;
    private readonly IProcessedEventLog _processed;

    /// <summary>Initializes a new instance of the <see cref="NotificationDispatchedHandler"/> class.</summary>
    /// <param name="bus">The event bus to announce delivery on.</param>
    /// <param name="connector">The outbound connector that performs delivery.</param>
    /// <param name="processed">The processed-event log for idempotency.</param>
    public NotificationDispatchedHandler(IEventBus bus, IOutboundConnector connector, IProcessedEventLog processed)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(connector);
        ArgumentNullException.ThrowIfNull(processed);
        _bus = bus;
        _connector = connector;
        _processed = processed;
    }

    /// <inheritdoc />
    public async Task HandleAsync(NotificationDispatched integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        // Not our transport: another outbound connector owns this dispatch.
        if (!string.Equals(integrationEvent.Transport, _connector.Transport, StringComparison.Ordinal))
        {
            return;
        }

        // Idempotent: deliver a given dispatch at most once.
        if (!_processed.TryMarkProcessed(integrationEvent.EventId))
        {
            return;
        }

        var result = await _connector.DeliverAsync(
            new OutboundMessage
            {
                Tenant = integrationEvent.Tenant,
                Channel = integrationEvent.Channel,
                Priority = integrationEvent.Priority,
                Subject = integrationEvent.Subject,
                Action = integrationEvent.Action,
                OccurredAt = integrationEvent.DispatchedAt,
            },
            cancellationToken).ConfigureAwait(false);

        await _bus.PublishAsync(
            new NotificationDelivered
            {
                Tenant = integrationEvent.Tenant,
                Transport = _connector.Transport,
                Channel = integrationEvent.Channel,
                Subject = integrationEvent.Subject,
                Delivered = result.Delivered,
                Detail = result.Detail,
                DeliveredAt = integrationEvent.DispatchedAt,
                SourceEventId = integrationEvent.EventId,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
