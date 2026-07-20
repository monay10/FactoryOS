using FactoryOS.Configuration.Providers;
using FactoryOS.Configuration.Reading;
using FactoryOS.Configuration.Secrets;
using FactoryOS.Configuration.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration entry point for the FactoryOS <b>Configuration</b> layer, which provides the
/// strongly-typed, validated, secret-expanded and reloadable configuration used across the platform.
/// </summary>
public static class ConfigurationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the reusable configuration building blocks: the secret provider, secret expander,
    /// tenant validator and tenant reader.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="configuration">The application configuration root.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddFactoryConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton<ISecretProvider>(_ => new EnvironmentVariableSecretProvider());
        services.TryAddSingleton<SecretExpander>();
        services.TryAddSingleton<ITenantConfigurationValidator, TenantConfigurationValidator>();
        services.TryAddSingleton<TenantConfigurationReader>();

        return services;
    }

    /// <summary>
    /// Wires a file-backed tenant configuration provider on top of the building blocks, loading and
    /// validating the given <c>tenant.json</c> eagerly.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="tenantConfigurationPath">The path to the tenant configuration file.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddTenantConfiguration(
        this IServiceCollection services,
        string tenantConfigurationPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantConfigurationPath);

        services.TryAddSingleton<ITenantConfigurationSource>(provider =>
            new JsonTenantConfigurationSource(
                tenantConfigurationPath,
                provider.GetRequiredService<TenantConfigurationReader>()));
        services.TryAddSingleton<ITenantConfigurationProvider, TenantConfigurationProvider>();

        return services;
    }
}
