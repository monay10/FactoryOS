using System.Globalization;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Notification.Domain;

namespace FactoryOS.Plugins.Notification.Application;

/// <summary>
/// The Notification module's consumer of <see cref="BrainAnswered"/>. When the Company Brain answers a question,
/// it routes the configured assistant channel to a transport, records the dispatch in the outbox and announces it
/// with <see cref="NotificationDispatched"/> — so an AI answer is delivered through the same door as any other
/// notification, without referencing the AI layer. Delivery over a real transport remains a connector's job.
/// Because the outbox is keyed by the answer event's id, redelivery neither records a duplicate nor re-announces.
/// </summary>
public sealed class BrainAnsweredHandler : IEventHandler<BrainAnswered>
{
    private readonly IEventBus _bus;
    private readonly INotificationOutbox _outbox;
    private readonly NotificationOptions _options;

    /// <summary>Initializes a new instance of the <see cref="BrainAnsweredHandler"/> class.</summary>
    /// <param name="bus">The event bus to announce dispatches on.</param>
    /// <param name="outbox">The notification outbox / read model.</param>
    /// <param name="options">The module options carrying the channel routing and assistant-notification settings.</param>
    public BrainAnsweredHandler(IEventBus bus, INotificationOutbox outbox, NotificationOptions options)
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
        BrainAnswered integrationEvent,
        EventContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var channel = _options.AssistantNotificationChannel;
        var transport = TransportResolver.Resolve(channel, _options);
        var subject = string.Format(
            CultureInfo.InvariantCulture,
            "Answer to '{0}': {1} ({2} source(s))",
            integrationEvent.Question,
            integrationEvent.Answer,
            integrationEvent.Citations.Count);

        var record = new NotificationRecord(
            channel,
            transport,
            _options.AssistantNotificationPriority,
            subject,
            _options.AssistantNotificationAction,
            integrationEvent.AnsweredAt);

        // Idempotent: the outbox is keyed by the answer's event id, so a duplicate is a no-op and we do not re-announce.
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
                Priority = _options.AssistantNotificationPriority,
                Subject = subject,
                Action = _options.AssistantNotificationAction,
                DispatchedAt = integrationEvent.AnsweredAt,
                SourceEventId = integrationEvent.EventId,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
