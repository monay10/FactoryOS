using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.DeliveryHealth.Domain;

namespace FactoryOS.Plugins.DeliveryHealth.Application;

/// <summary>
/// The Delivery Health module's consumer of <see cref="NotificationDelivered"/>. It folds each delivery outcome
/// into the per-tenant, per-transport tallies and the recent-failure list — closing the notification audit trail on
/// the read side (dispatched → delivered → observed) — and, when a transport's consecutive-failure streak reaches
/// the configured threshold, raises <see cref="DeliveryHealthDegraded"/> exactly once per crossing. It references
/// no other module, only the shared event vocabulary. Because the store is keyed by the delivery event's id,
/// redelivery neither double-counts nor re-alerts.
/// </summary>
public sealed class NotificationDeliveredHandler : IEventHandler<NotificationDelivered>
{
    private readonly IEventBus _bus;
    private readonly IDeliveryHealthStore _store;
    private readonly int _threshold;

    /// <summary>Initializes a new instance of the <see cref="NotificationDeliveredHandler"/> class.</summary>
    /// <param name="bus">The event bus to raise degradation alerts on.</param>
    /// <param name="store">The delivery-health read model to fold outcomes into.</param>
    /// <param name="options">The module options carrying the failure threshold.</param>
    public NotificationDeliveredHandler(IEventBus bus, IDeliveryHealthStore store, DeliveryHealthOptions options)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(options);
        _bus = bus;
        _store = store;
        _threshold = Math.Max(1, options.FailureThreshold);
    }

    /// <inheritdoc />
    public async Task HandleAsync(NotificationDelivered integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var outcome = _store.Record(
            integrationEvent.Tenant,
            integrationEvent.EventId,
            integrationEvent.Transport,
            integrationEvent.Channel,
            integrationEvent.Subject,
            integrationEvent.Delivered,
            integrationEvent.Detail,
            integrationEvent.DeliveredAt);

        // Alert exactly once per crossing: only when this delivery is the one that reaches the threshold. A further
        // failure (streak past the threshold) does not re-alert; a success resets the streak so it can alert again.
        if (outcome is { Recorded: true } && !integrationEvent.Delivered && outcome.ConsecutiveFailures == _threshold)
        {
            await _bus.PublishAsync(
                new DeliveryHealthDegraded
                {
                    Tenant = integrationEvent.Tenant,
                    Transport = integrationEvent.Transport,
                    ConsecutiveFailures = outcome.ConsecutiveFailures,
                    Attempts = outcome.Attempts,
                    Failed = outcome.Failed,
                    LastDetail = integrationEvent.Detail,
                    DetectedAt = integrationEvent.DeliveredAt,
                    SourceEventId = integrationEvent.EventId,
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
