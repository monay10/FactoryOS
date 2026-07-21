using FactoryOS.Connectors.Framework.Health;
using FactoryOS.Connectors.Framework.Registry;
using FactoryOS.Connectors.Framework.Runtime;

namespace FactoryOS.Connectors.Framework.Catalog;

/// <summary>
/// A read model over the registered connectors: their metadata, capability index and health. The catalog is
/// the query surface a management UI reads; it never mutates connector state.
/// </summary>
public interface IConnectorCatalog
{
    /// <summary>Lists the metadata of every registered connector.</summary>
    /// <returns>The connector metadata.</returns>
    IReadOnlyCollection<ConnectorMetadata> List();

    /// <summary>Finds a connector's metadata by key.</summary>
    /// <param name="key">The connector key.</param>
    /// <returns>The metadata, or <see langword="null"/> when no connector has that key.</returns>
    ConnectorMetadata? Find(string key);

    /// <summary>Lists the connectors that declare a given capability.</summary>
    /// <param name="capability">The capability to filter by.</param>
    /// <returns>The matching connectors' metadata.</returns>
    IReadOnlyCollection<ConnectorMetadata> WithCapability(ConnectorCapability capability);

    /// <summary>Gets the health snapshot of every tracked connector.</summary>
    /// <returns>The health snapshots.</returns>
    IReadOnlyCollection<ConnectorHealth> Health();
}

/// <summary>Default <see cref="IConnectorCatalog"/> projecting the registry and health service.</summary>
public sealed class ConnectorCatalog : IConnectorCatalog
{
    private readonly IConnectorRegistry _registry;
    private readonly IConnectorHealthService _health;

    /// <summary>Initializes a new instance of the <see cref="ConnectorCatalog"/> class.</summary>
    /// <param name="registry">The connector registry.</param>
    /// <param name="health">The connector health service.</param>
    public ConnectorCatalog(IConnectorRegistry registry, IConnectorHealthService health)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(health);
        _registry = registry;
        _health = health;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<ConnectorMetadata> List() =>
        _registry.All.Select(ConnectorMetadata.FromDescriptor).ToArray();

    /// <inheritdoc />
    public ConnectorMetadata? Find(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var descriptor = _registry.Find(key);
        return descriptor is null ? null : ConnectorMetadata.FromDescriptor(descriptor);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<ConnectorMetadata> WithCapability(ConnectorCapability capability) =>
        _registry.All
            .Where(descriptor => descriptor.Supports(capability))
            .Select(ConnectorMetadata.FromDescriptor)
            .ToArray();

    /// <inheritdoc />
    public IReadOnlyCollection<ConnectorHealth> Health() => _health.All();
}
