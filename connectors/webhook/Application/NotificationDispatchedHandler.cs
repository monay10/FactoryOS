using FactoryOS.Contracts.Connectors;
using FactoryOS.Contracts.Events;

namespace FactoryOS.Connectors.Webhook.Application;

/// <summary>
/// The bus bridge that drives the webhook connector. It consumes <see cref="NotificationDispatched"/>, ignores
/// dispatches on other transports, POSTs the ones on its own transport, and announces the outcome with
/// <see cref="NotificationDelivered"/>. Depending on the concrete <see cref="WebhookConnector"/> (not the shared
/// <see cref="IOutboundConnector"/>) lets it run alongside other outbound connectors without ambiguity — each
/// bridge drives its own transport. Deduplicated by the dispatch's event id.
/// </summary>
public sealed class NotificationDispatchedHandler : IEventHandler<NotificationDispatched>
{
    private readonly IEventBus _bus;
    private readonly WebhookConnector _connector;
    private readonly IProcessedEventLog _processed;

    /// <summary>Initializes a new instance of the <see cref="NotificationDispatchedHandler"/> class.</summary>
    /// <param name="bus">The event bus to announce delivery on.</param>
    /// <param name="connector">The webhook connector that performs delivery.</param>
    /// <param name="processed">The processed-event log for idempotency.</param>
    public NotificationDispatchedHandler(IEventBus bus, WebhookConnector connector, IProcessedEventLog processed)
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
