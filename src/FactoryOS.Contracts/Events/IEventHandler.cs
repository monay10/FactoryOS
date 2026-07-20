namespace FactoryOS.Contracts.Events;

/// <summary>
/// Handles integration events of type <typeparamref name="TEvent"/> delivered by the event bus.
/// Handlers must be idempotent, since delivery is at-least-once.
/// </summary>
/// <typeparam name="TEvent">The integration event type handled.</typeparam>
public interface IEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    /// <summary>Handles a single integration event.</summary>
    /// <param name="integrationEvent">The event to handle.</param>
    /// <param name="context">The delivery context (correlation, tracing, attempt).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the event has been handled.</returns>
    Task HandleAsync(TEvent integrationEvent, EventContext context, CancellationToken cancellationToken);
}
