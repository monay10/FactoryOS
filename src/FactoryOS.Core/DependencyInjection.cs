namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration entry point for the FactoryOS <b>Core</b> platform kernel. Core hosts the
/// platform services and, by design, references no business module.
/// </summary>
public static class CoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Core platform services, including the plugin framework, into the container.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddPluginFramework();
        services.AddConnectorFramework();
        services.AddIotHub();
        return services;
    }
}
