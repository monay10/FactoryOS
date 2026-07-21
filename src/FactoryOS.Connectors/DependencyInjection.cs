using FactoryOS.Connectors.Binding;
using FactoryOS.Connectors.Deduplication;
using FactoryOS.Connectors.Framework.Activation;
using FactoryOS.Connectors.Framework.Catalog;
using FactoryOS.Connectors.Framework.Configuration;
using FactoryOS.Connectors.Framework.Health;
using FactoryOS.Connectors.Framework.Hosting;
using FactoryOS.Connectors.Framework.Management;
using FactoryOS.Connectors.Framework.Registry;
using FactoryOS.Connectors.Framework.Security;
using FactoryOS.Connectors.Normalization;
using FactoryOS.Connectors.Pipeline;
using FactoryOS.Connectors.Transforms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration entry point for the FactoryOS <b>Connector</b> framework — the normalize/dedup pipeline
/// that turns raw source records into the Standard Model.
/// </summary>
public static class ConnectorServiceCollectionExtensions
{
    /// <summary>Registers the connector framework services into the dependency-injection container.</summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddConnectorFramework(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions();

        // The normalize/dedup ingestion pipeline.
        services.TryAddSingleton<IValueTransformer, ValueTransformer>();
        services.TryAddSingleton<IRecordNormalizer, RecordNormalizer>();
        services.TryAddSingleton<IRecordDeduplicator, RecordDeduplicator>();
        services.TryAddSingleton<IStandardEntityBinder, StandardEntityBinder>();
        services.TryAddSingleton<IIngestionPipeline, IngestionPipeline>();

        // The connector platform: registry, catalog, manager, host, activation, configuration and health.
        services.TryAddSingleton<IConnectorSecretProtector>(static provider =>
        {
            var security = provider.GetRequiredService<IOptions<ConnectorOptions>>().Value.Security;
            return string.IsNullOrWhiteSpace(security.EncryptionKey)
                ? new PassthroughConnectorSecretProtector()
                : AesGcmConnectorSecretProtector.FromBase64Key(security.EncryptionKey);
        });
        services.TryAddSingleton<IConnectorRegistry, ConnectorRegistry>();
        services.TryAddSingleton<IConnectorActivator, ConnectorActivator>();
        services.TryAddSingleton<IConnectorConfigurationProvider, ConnectorConfigurationProvider>();
        services.TryAddSingleton<IConnectorHealthService, ConnectorHealthService>();
        services.TryAddSingleton<IConnectorCatalog, ConnectorCatalog>();
        services.TryAddSingleton<IConnectorManager, ConnectorManager>();
        services.TryAddSingleton<IConnectorHost, ConnectorHost>();

        return services;
    }

    /// <summary>
    /// Registers the connector framework and binds its options from the <c>Connectors</c> configuration
    /// section (including the nested <c>Discovery</c>, <c>Health</c> and <c>Security</c> sections).
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="configuration">The application configuration root.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddConnectorFramework(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddConnectorFramework();
        services.Configure<ConnectorOptions>(configuration.GetSection(ConnectorConstants.ConfigurationSection));

        return services;
    }
}
