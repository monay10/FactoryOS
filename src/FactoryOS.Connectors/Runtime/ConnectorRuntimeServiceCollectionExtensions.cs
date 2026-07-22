using FactoryOS.Connectors.Runtime.Configuration;
using FactoryOS.Connectors.Runtime.Discovery;
using FactoryOS.Connectors.Runtime.Domain;
using FactoryOS.Connectors.Runtime.Events;
using FactoryOS.Connectors.Runtime.Execution;
using FactoryOS.Connectors.Runtime.Health;
using FactoryOS.Connectors.Runtime.Integration;
using FactoryOS.Connectors.Runtime.Persistence;
using FactoryOS.Connectors.Runtime.Pipeline;
using FactoryOS.Connectors.Runtime.Security;
using FactoryOS.Domain.Abstractions;
using FactoryOS.Domain.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration entry point for the FactoryOS <b>connector runtime</b> — the invocation layer above the
/// connector framework.
/// </summary>
public static class ConnectorRuntimeServiceCollectionExtensions
{
    /// <summary>
    /// Registers the connector runtime and the connector framework it builds on.
    /// <para>
    /// Everything is registered through <c>TryAdd</c>, so a host that has already supplied its own
    /// repository, store, authorizer, secret source or clock keeps it. The pipeline stages are registered as
    /// an enumerable and ordered by the stage itself, never by the order of these lines.
    /// </para>
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddConnectorRuntime(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddConnectorFramework();
        services.AddOptions();

        services.TryAddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        // Persistence.
        services.TryAddSingleton<IConnectorRepository, InMemoryConnectorRepository>();
        services.TryAddSingleton<IConnectorStore, InMemoryConnectorStore>();
        services.TryAddSingleton<IConnectorConfigurationRepository, InMemoryConnectorConfigurationRepository>();
        services.TryAddSingleton<IConnectorCredentialStore, InMemoryConnectorCredentialStore>();

        // Events and observability ports; all three fan out to every registered subscriber. The in-memory
        // sink is the default only when the host has registered none of its own — see TryAddDefaultSink.
        services.TryAddSingleton<InMemoryConnectorRuntimeEventSink>();
        TryAddDefaultSink<IConnectorRuntimeEventSink, InMemoryConnectorRuntimeEventSink>(services);
        services.TryAddSingleton<ConnectorRuntimePublisher>();
        services.TryAddSingleton<InMemoryConnectorAuditSink>();
        TryAddDefaultSink<IConnectorAuditSink, InMemoryConnectorAuditSink>(services);
        services.TryAddSingleton<ConnectorAuditPublisher>();
        services.TryAddSingleton<InMemoryConnectorMetricSink>();
        TryAddDefaultSink<IConnectorMetricSink, InMemoryConnectorMetricSink>(services);
        services.TryAddSingleton<ConnectorMetricPublisher>();

        // Security: the authorizer is a port a host replaces with its own decision layer.
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IConnectorSecretSource, EnvironmentConnectorSecretSource>());
        services.TryAddSingleton<ConnectorSecretResolver>();
        services.TryAddSingleton<IConnectorAuthorizer, PermissionConnectorAuthorizer>();

        // Discovery.
        services.TryAddSingleton<IConnectorDiscovery, ConnectorDiscovery>();
        services.TryAddSingleton<CapabilityResolver>();
        services.TryAddSingleton<VersionResolver>();
        services.TryAddSingleton<CompatibilityValidator>();

        // Resilience.
        services.TryAddSingleton<IConnectorDelay, TaskConnectorDelay>();
        services.TryAddSingleton<RetryEngine>();
        services.TryAddSingleton<CircuitBreakerEngine>();
        services.TryAddSingleton<RateLimiter>();
        services.TryAddSingleton<ConnectorResponseCache>();

        // The pipeline. Stages sort themselves by ConnectorPipelineOrder, so these lines may be reordered
        // freely without changing what the pipeline does.
        services.TryAddEnumerable(
        [
            ServiceDescriptor.Singleton<IConnectorMiddleware, TracingMiddleware>(),
            ServiceDescriptor.Singleton<IConnectorMiddleware, MetricsMiddleware>(),
            ServiceDescriptor.Singleton<IConnectorMiddleware, MonitoringMiddleware>(),
            ServiceDescriptor.Singleton<IConnectorMiddleware, AuditMiddleware>(),
            ServiceDescriptor.Singleton<IConnectorMiddleware, AuthorizationMiddleware>(),
            ServiceDescriptor.Singleton<IConnectorMiddleware, ValidationMiddleware>(),
            ServiceDescriptor.Singleton<IConnectorMiddleware, AuthenticationMiddleware>(),
            ServiceDescriptor.Singleton<IConnectorMiddleware, CachingMiddleware>(),
            ServiceDescriptor.Singleton<IConnectorMiddleware, RetryMiddleware>(),
            ServiceDescriptor.Singleton<IConnectorMiddleware, RateLimitMiddleware>(),
            ServiceDescriptor.Singleton<IConnectorMiddleware, CircuitBreakerMiddleware>(),
            ServiceDescriptor.Singleton<IConnectorMiddleware, TransformationMiddleware>(),
        ]);
        services.TryAddSingleton<ConnectorPipeline>();

        // Execution.
        services.TryAddSingleton<ConnectorMetrics>();
        services.TryAddSingleton<ConnectorSessionManager>();
        services.TryAddSingleton<ConnectorInvoker>();
        services.TryAddSingleton<ConnectorDispatcher>();
        services.TryAddSingleton<ConnectorInstanceRegistry>();
        services.TryAddSingleton<ConnectorLoader>();
        services.TryAddSingleton<ConnectorRuntime>();
        services.TryAddSingleton<ConnectorRuntimeHost>();
        services.TryAddSingleton<ConnectorScheduler>();
        services.TryAddSingleton<ConnectorHealthEngine>();
        services.TryAddSingleton<ConnectorEngine>();

        return services;
    }

    /// <summary>
    /// Registers the connector runtime and binds its options from the <c>Connectors:Runtime</c> configuration
    /// section, along with the connector framework's own <c>Connectors</c> section.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="configuration">The application configuration root.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddConnectorRuntime(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddConnectorFramework(configuration);
        services.AddConnectorRuntime();
        services.Configure<ConnectorRuntimeOptions>(
            configuration.GetSection(ConnectorRuntimeConstants.ConfigurationSection));

        return services;
    }

    /// <summary>
    /// Registers the in-memory sink as the fan-out default, but only when the host has registered no sink of
    /// its own. It resolves the same singleton the concrete type does, so a host that reads the in-memory
    /// history back sees exactly what the pipeline wrote — registering the implementation type twice would
    /// produce two instances and one of them would silently be the empty one.
    /// </summary>
    private static void TryAddDefaultSink<TService, TImplementation>(IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(TService)))
        {
            return;
        }

        services.AddSingleton<TService>(provider => provider.GetRequiredService<TImplementation>());
    }
}
