using FactoryOS.Domain.Results;
using FactoryOS.Plugins.Runtime.Configuration;
using FactoryOS.Plugins.Runtime.Discovery;
using FactoryOS.Plugins.Runtime.Domain;
using FactoryOS.Plugins.Runtime.Health;
using Microsoft.Extensions.Options;

namespace FactoryOS.Plugins.Runtime.Execution;

/// <summary>
/// The single surface the rest of the platform uses to work with plugins.
/// <para>
/// This is what the architecture rule means in code. An engine — workflow, forms, approval, notification —
/// never references a plugin, a package or a concrete plugin class; it asks <see cref="IPluginRuntime"/> what
/// extends it and gets back <b>data</b>. That is the whole reason a plugin can be added, updated or removed
/// without an engine changing.
/// </para>
/// </summary>
public interface IPluginRuntime
{
    /// <summary>Scans the configured package root for plugin packages.</summary>
    /// <param name="rootDirectory">The root to scan, or <see langword="null"/> to use the configured one.</param>
    /// <returns>What was found, and what was rejected with the reason.</returns>
    PluginDiscoveryResult Discover(string? rootDirectory = null);

    /// <summary>Installs a package for the caller's tenant.</summary>
    /// <param name="caller">Who is asking.</param>
    /// <param name="package">The package.</param>
    /// <param name="granted">The permissions the tenant grants it.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The installation, or a failure.</returns>
    Task<Result<PluginInstance>> InstallAsync(
        PluginCaller caller,
        PluginPackage package,
        IEnumerable<PluginPermission> granted,
        CancellationToken cancellationToken = default);

    /// <summary>Lists what a tenant has installed.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The installations.</returns>
    IReadOnlyList<PluginInstance> Installed(string tenant);

    /// <summary>Lists what currently extends one published extension point, for one tenant.</summary>
    /// <param name="tenant">The tenant asking.</param>
    /// <param name="kind">The extension point.</param>
    /// <returns>The contributions in service.</returns>
    IReadOnlyList<PluginExtension> Extensions(string tenant, PluginExtensionPointKind kind);

    /// <summary>Takes a health report for one tenant's plugin.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="pluginKey">The plugin key.</param>
    /// <returns>The report.</returns>
    PluginHealthReport Health(string tenant, string pluginKey);

    /// <summary>Gets the lifecycle manager driving install, load, start, stop, suspend, resume and remove.</summary>
    IPluginLifecycleManager Lifecycle { get; }

    /// <summary>Gets the manager that moves a plugin between versions.</summary>
    PluginUpdateManager Updates { get; }

    /// <summary>Gets the manager that changes settings, grants and quotas.</summary>
    PluginConfigurationManager Configuration { get; }
}

/// <summary>Default <see cref="IPluginRuntime"/>, composing the parts the runtime is made of.</summary>
public sealed class PluginRuntime : IPluginRuntime
{
    private readonly IPluginPackageDiscovery _discovery;
    private readonly PluginInstanceRegistry _registry;
    private readonly PluginExtensionPointResolver _extensions;
    private readonly PluginHealthEngine _health;
    private readonly PluginRuntimeOptions _options;

    /// <summary>Initializes a new instance of the <see cref="PluginRuntime"/> class.</summary>
    /// <param name="discovery">The package discovery.</param>
    /// <param name="registry">The instance registry.</param>
    /// <param name="extensions">The extension-point resolver.</param>
    /// <param name="health">The health engine.</param>
    /// <param name="lifecycle">The lifecycle manager.</param>
    /// <param name="updates">The update manager.</param>
    /// <param name="configuration">The configuration manager.</param>
    /// <param name="options">The runtime options.</param>
    public PluginRuntime(
        IPluginPackageDiscovery discovery,
        PluginInstanceRegistry registry,
        PluginExtensionPointResolver extensions,
        PluginHealthEngine health,
        IPluginLifecycleManager lifecycle,
        PluginUpdateManager updates,
        PluginConfigurationManager configuration,
        IOptions<PluginRuntimeOptions> options)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(extensions);
        ArgumentNullException.ThrowIfNull(health);
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(updates);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(options);

        _discovery = discovery;
        _registry = registry;
        _extensions = extensions;
        _health = health;
        _options = options.Value;

        Lifecycle = lifecycle;
        Updates = updates;
        Configuration = configuration;
    }

    /// <inheritdoc />
    public IPluginLifecycleManager Lifecycle { get; }

    /// <inheritdoc />
    public PluginUpdateManager Updates { get; }

    /// <inheritdoc />
    public PluginConfigurationManager Configuration { get; }

    /// <inheritdoc />
    public PluginDiscoveryResult Discover(string? rootDirectory = null) =>
        _discovery.Discover(rootDirectory ?? _options.PackageRoot);

    /// <inheritdoc />
    public Task<Result<PluginInstance>> InstallAsync(
        PluginCaller caller,
        PluginPackage package,
        IEnumerable<PluginPermission> granted,
        CancellationToken cancellationToken = default) =>
        Lifecycle.InstallAsync(caller, package, granted, cancellationToken);

    /// <inheritdoc />
    public IReadOnlyList<PluginInstance> Installed(string tenant) => _registry.ForTenant(tenant);

    /// <inheritdoc />
    public IReadOnlyList<PluginExtension> Extensions(string tenant, PluginExtensionPointKind kind) =>
        _extensions.Resolve(tenant, kind);

    /// <inheritdoc />
    public PluginHealthReport Health(string tenant, string pluginKey) => _health.Check(tenant, pluginKey);
}
