using FactoryOS.Domain.Abstractions;
using FactoryOS.Domain.Time;
using FactoryOS.Plugins.Runtime.Configuration;
using FactoryOS.Plugins.Runtime.Discovery;
using FactoryOS.Plugins.Runtime.Events;
using FactoryOS.Plugins.Runtime.Execution;
using FactoryOS.Plugins.Runtime.Health;
using FactoryOS.Plugins.Runtime.Integration;
using FactoryOS.Plugins.Runtime.Isolation;
using FactoryOS.Plugins.Runtime.Persistence;
using FactoryOS.Plugins.Runtime.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration entry point for the FactoryOS <b>plugin runtime</b> — the tenant-scoped extension layer above
/// the plugin framework.
/// </summary>
public static class PluginRuntimeServiceCollectionExtensions
{
    /// <summary>
    /// Registers the plugin runtime and the plugin framework it builds on.
    /// <para>
    /// Everything goes through <c>TryAdd</c>, so a host that has already supplied its own store, repository,
    /// authorizer, signing keys or clock keeps it.
    /// </para>
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddPluginRuntime(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddPluginFramework();
        services.AddOptions();

        services.TryAddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        // Persistence.
        services.TryAddSingleton<IPluginRepository, InMemoryPluginRepository>();
        services.TryAddSingleton<IPluginStore, InMemoryPluginStore>();
        services.TryAddSingleton<IPluginManifestRepository, InMemoryPluginManifestRepository>();
        services.TryAddSingleton<IPluginPackageStore, InMemoryPluginPackageStore>();

        // Events and observability ports. All three fan out to every registered sink; the in-memory default
        // is used only when the host has registered none of its own.
        services.TryAddSingleton<InMemoryPluginRuntimeEventSink>();
        TryAddDefaultSink<IPluginRuntimeEventSink, InMemoryPluginRuntimeEventSink>(services);
        services.TryAddSingleton<PluginRuntimePublisher>();
        services.TryAddSingleton<InMemoryPluginAuditSink>();
        TryAddDefaultSink<IPluginAuditSink, InMemoryPluginAuditSink>(services);
        services.TryAddSingleton<PluginAuditPublisher>();
        services.TryAddSingleton<InMemoryPluginMetricSink>();
        TryAddDefaultSink<IPluginMetricSink, InMemoryPluginMetricSink>(services);
        services.TryAddSingleton<PluginMetricPublisher>();
        services.TryAddSingleton<PluginRuntimeAnnouncer>();

        // Security. The authorizer and the signing keys are ports a host replaces.
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IPluginSigningKeySource, EnvironmentPluginSigningKeySource>());
        services.TryAddSingleton<IPluginAuthorizer, PermissionPluginAuthorizer>();
        services.TryAddSingleton<PluginAuthorizationGate>();
        services.TryAddSingleton<PluginSignatureValidator>();
        services.TryAddSingleton<PluginManifestValidator>();
        services.TryAddSingleton<PluginPermissionValidator>();
        services.TryAddSingleton<PluginCapabilityRequirementValidator>();
        services.TryAddSingleton<PluginValidationSuite>();

        // Discovery and resolution.
        services.TryAddSingleton<PluginPackageReader>();
        services.TryAddSingleton<IPluginPackageDiscovery, PluginRuntimeDiscovery>();
        services.TryAddSingleton<PluginVersionResolver>();
        services.TryAddSingleton<PluginRuntimeDependencyResolver>();
        services.TryAddSingleton<PluginCompatibilityValidator>();
        services.TryAddSingleton<PluginExtensionPointResolver>();

        // Isolation.
        services.TryAddSingleton<PluginIsolationManager>();
        services.TryAddSingleton<PluginSandbox>();

        // Execution.
        services.TryAddSingleton<PluginInstanceRegistry>();
        services.TryAddSingleton<PluginPackageLoader>();
        services.TryAddSingleton<IPluginLifecycleManager, PluginLifecycleManager>();
        services.TryAddSingleton<PluginUpdateManager>();
        services.TryAddSingleton<PluginConfigurationManager>();
        services.TryAddSingleton<PluginHealthEngine>();
        services.TryAddSingleton<PluginRuntimeScheduler>();
        services.TryAddSingleton<PluginRuntimeHost>();
        services.TryAddSingleton<IPluginRuntime, PluginRuntime>();
        services.TryAddSingleton<PluginEngine>();

        return services;
    }

    /// <summary>
    /// Registers the plugin runtime and binds its options from the <c>Plugins:Runtime</c> configuration
    /// section, alongside the framework's own <c>Plugins</c> section.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="configuration">The application configuration root.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, to allow chaining.</returns>
    public static IServiceCollection AddPluginRuntime(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddPluginFramework(configuration);
        services.AddPluginRuntime();
        services.Configure<PluginRuntimeOptions>(
            configuration.GetSection(PluginRuntimeConstants.ConfigurationSection));

        return services;
    }

    /// <summary>
    /// Registers the in-memory sink as the default for a port, but only when the host has registered nothing
    /// for it. Using <c>TryAddEnumerable</c> here would add a second registration alongside the host's own,
    /// leaving the runtime writing into a sink nobody reads.
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
