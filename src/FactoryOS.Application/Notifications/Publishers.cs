using FactoryOS.Contracts.Events;

namespace FactoryOS.Application.Notifications;

/// <summary>Publishes in-process notifications to any registered subscribers.</summary>
public interface INotificationPublisher
{
    /// <summary>Publishes a notification.</summary>
    /// <typeparam name="TNotification">The notification type.</typeparam>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when subscribers have been notified.</returns>
    Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : notnull;
}

/// <summary>Publishes domain events raised by aggregates during a unit of work.</summary>
public interface IDomainEventPublisher
{
    /// <summary>Publishes a domain event.</summary>
    /// <typeparam name="TDomainEvent">The domain-event type.</typeparam>
    /// <param name="domainEvent">The domain event to publish.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the event has been dispatched.</returns>
    Task PublishAsync<TDomainEvent>(TDomainEvent domainEvent, CancellationToken cancellationToken = default)
        where TDomainEvent : notnull;
}

/// <summary>Publishes integration events onto the event bus for other modules to consume.</summary>
public interface IIntegrationEventPublisher
{
    /// <summary>Publishes an integration event.</summary>
    /// <param name="integrationEvent">The integration event to publish.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the event has been published.</returns>
    Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
}
