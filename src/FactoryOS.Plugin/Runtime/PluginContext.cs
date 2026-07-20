using FactoryOS.Contracts.Plugins;
using FactoryOS.Plugin.Configuration;

namespace FactoryOS.Plugin.Runtime;

/// <summary>
/// The runtime context handed to a plugin during its lifecycle: its manifest, on-disk location and its
/// own configuration. A plugin reads its context rather than reaching into the host.
/// </summary>
public interface IPluginContext
{
    /// <summary>Gets the plugin key.</summary>
    string Key { get; }

    /// <summary>Gets the plugin manifest.</summary>
    PluginManifest Manifest { get; }

    /// <summary>Gets the plugin's on-disk location, if any.</summary>
    string? Location { get; }

    /// <summary>Gets the plugin's configuration.</summary>
    PluginConfiguration Configuration { get; }
}

/// <summary>Default <see cref="IPluginContext"/> built from a descriptor and the plugin's configuration.</summary>
public sealed class PluginContext : IPluginContext
{
    /// <summary>Initializes a new instance of the <see cref="PluginContext"/> class.</summary>
    /// <param name="descriptor">The plugin descriptor.</param>
    /// <param name="configuration">The plugin's configuration.</param>
    public PluginContext(PluginDescriptor descriptor, PluginConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(configuration);

        Manifest = descriptor.Manifest;
        Location = descriptor.Location;
        Configuration = configuration;
    }

    /// <inheritdoc />
    public string Key => Manifest.Key;

    /// <inheritdoc />
    public PluginManifest Manifest { get; }

    /// <inheritdoc />
    public string? Location { get; }

    /// <inheritdoc />
    public PluginConfiguration Configuration { get; }
}
