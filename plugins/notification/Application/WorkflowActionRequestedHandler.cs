using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Notification.Domain;

namespace FactoryOS.Plugins.Notification.Application;

/// <summary>
/// The Notification module's consumer of <see cref="WorkflowActionRequested"/>. It routes the action's channel
/// to a transport (per the tenant's configured routing), records the dispatch intent in the outbox, and
/// announces it with <see cref="NotificationDispatched"/> — leaving actual delivery to a transport connector.
/// It references no other module, only the shared event vocabulary. Because the outbox is keyed by the action's
/// event id, redelivery of the same action neither records a duplicate nor re-announces.
/// </summary>
public sealed class WorkflowActionRequestedHandler : IEventHandler<WorkflowActionRequested>
{
    private readonly IEventBus _bus;
    private readonly INotificationOutbox _outbox;
    private readonly NotificationOptions _options;

    /// <summary>Initializes a new instance of the <see cref="WorkflowActionRequestedHandler"/> class.</summary>
    /// <param name="bus">The event bus to announce dispatches on.</param>
    /// <param name="outbox">The notification outbox / read model.</param>
    /// <param name="options">The module options carrying the channel routing.</param>
    public WorkflowActionRequestedHandler(IEventBus bus, INotificationOutbox outbox, NotificationOptions options)
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
        WorkflowActionRequested integrationEvent,
        EventContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var transport = TransportResolver.Resolve(integrationEvent.Channel, _options);
        var record = new NotificationRecord(
            integrationEvent.Channel,
            transport,
            integrationEvent.Priority,
            integrationEvent.Subject,
            integrationEvent.Action,
            integrationEvent.OccurredAt);

        // Idempotent: the outbox is keyed by the action's event id, so a duplicate is a no-op and we do not re-announce.
        if (!_outbox.TryRecord(integrationEvent.Tenant, integrationEvent.EventId, record))
        {
            return;
        }

        await _bus.PublishAsync(
            new NotificationDispatched
            {
                Tenant = integrationEvent.Tenant,
                Channel = integrationEvent.Channel,
                Transport = transport,
                Priority = integrationEvent.Priority,
                Subject = integrationEvent.Subject,
                Action = integrationEvent.Action,
                DispatchedAt = integrationEvent.OccurredAt,
                SourceEventId = integrationEvent.EventId,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
