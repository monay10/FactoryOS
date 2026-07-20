using FactoryOS.Contracts.Events;
using FactoryOS.EventBus.InProcess;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration entry point for the FactoryOS <b>Event Bus</b> layer, the messaging backbone over
/// which modules communicate without direct references.
/// </summary>
public static class EventBusServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-process event bus, its dead-letter queue, metrics and options into the container.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddEventBus(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<EventBusOptions>();
        services.TryAddSingleton<IDeadLetterQueue, InMemoryDeadLetterQueue>();
        services.TryAddSingleton<IEventBusMetrics, InMemoryEventBusMetrics>();
        services.TryAddSingleton<IEventBus, InProcessEventBus>();

        return services;
    }
}
