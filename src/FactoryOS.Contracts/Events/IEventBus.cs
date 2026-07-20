namespace FactoryOS.Contracts.Events;

/// <summary>
/// The event bus: the only channel through which modules communicate. Modules publish and subscribe
/// to integration events and never reference one another directly.
/// </summary>
public interface IEventBus
{
    /// <summary>Publishes an integration event to all registered handlers.</summary>
    /// <typeparam name="TEvent">The integration event type.</typeparam>
    /// <param name="integrationEvent">The event to publish.</param>
    /// <param name="options">Optional publish metadata; defaults to <see cref="EventPublishOptions.Default"/>.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when publishing has finished.</returns>
    Task PublishAsync<TEvent>(
        TEvent integrationEvent,
        EventPublishOptions? options = null,
        CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent;
}
