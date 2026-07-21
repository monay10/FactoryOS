using FactoryOS.Connectors.Framework.Configuration;
using FactoryOS.Contracts.Connectors;

namespace FactoryOS.Connectors.Framework.Runtime;

/// <summary>
/// The runtime context handed to a connector during its lifecycle: its manifest, version, capabilities,
/// on-disk location and its own (secret-aware) configuration. A connector reads its context rather than
/// reaching into the host.
/// </summary>
public interface IConnectorContext
{
    /// <summary>Gets the connector key.</summary>
    string Key { get; }

    /// <summary>Gets the connector manifest.</summary>
    ConnectorManifest Manifest { get; }

    /// <summary>Gets the connector version.</summary>
    ConnectorVersion Version { get; }

    /// <summary>Gets the declared capabilities.</summary>
    ConnectorCapability Capabilities { get; }

    /// <summary>Gets the connector's on-disk location, if any.</summary>
    string? Location { get; }

    /// <summary>Gets the connector's configuration.</summary>
    ConnectorConfiguration Configuration { get; }
}

/// <summary>Default <see cref="IConnectorContext"/> built from a descriptor and the connector's configuration.</summary>
public sealed class ConnectorContext : IConnectorContext
{
    /// <summary>Initializes a new instance of the <see cref="ConnectorContext"/> class.</summary>
    /// <param name="descriptor">The connector descriptor.</param>
    /// <param name="configuration">The connector's configuration.</param>
    public ConnectorContext(ConnectorDescriptor descriptor, ConnectorConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(configuration);

        Manifest = descriptor.Manifest;
        Version = descriptor.Version;
        Capabilities = descriptor.Capabilities;
        Location = descriptor.Location;
        Configuration = configuration;
    }

    /// <inheritdoc />
    public string Key => Manifest.Key;

    /// <inheritdoc />
    public ConnectorManifest Manifest { get; }

    /// <inheritdoc />
    public ConnectorVersion Version { get; }

    /// <inheritdoc />
    public ConnectorCapability Capabilities { get; }

    /// <inheritdoc />
    public string? Location { get; }

    /// <inheritdoc />
    public ConnectorConfiguration Configuration { get; }
}
