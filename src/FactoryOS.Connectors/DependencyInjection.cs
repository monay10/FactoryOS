using FactoryOS.Connectors.Binding;
using FactoryOS.Connectors.Deduplication;
using FactoryOS.Connectors.Normalization;
using FactoryOS.Connectors.Pipeline;
using FactoryOS.Connectors.Transforms;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

        services.TryAddSingleton<IValueTransformer, ValueTransformer>();
        services.TryAddSingleton<IRecordNormalizer, RecordNormalizer>();
        services.TryAddSingleton<IRecordDeduplicator, RecordDeduplicator>();
        services.TryAddSingleton<IStandardEntityBinder, StandardEntityBinder>();
        services.TryAddSingleton<IIngestionPipeline, IngestionPipeline>();

        return services;
    }
}
