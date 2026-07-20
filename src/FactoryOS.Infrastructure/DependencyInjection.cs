using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Composition root for the FactoryOS <b>Infrastructure</b> layer. Wires together every
/// infrastructure-side concern (configuration, persistence, identity, event bus and the core
/// platform) behind a single registration call so that the host only depends on Application
/// and this entry point.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers all infrastructure services into the dependency-injection container.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="configuration">The application configuration root.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddInfrastructureFoundation(configuration);
        services.AddFactoryConfiguration(configuration);
        services.AddPersistence(configuration);
        services.AddIdentityModule(configuration);
        services.AddEventBus();
        services.AddCore();
        services.AddLlmGateway(configuration);
        services.AddEmbeddingGateway(configuration);
        services.AddKnowledgeBase();
        services.AddPromptEngine();
        services.AddCompanyBrain();
        services.AddAgentFramework();

        return services;
    }
}
