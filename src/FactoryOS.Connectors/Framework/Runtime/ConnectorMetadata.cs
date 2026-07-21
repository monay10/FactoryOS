using FactoryOS.Contracts.Connectors;

namespace FactoryOS.Connectors.Framework.Runtime;

/// <summary>
/// The read-model projection of a connector: its stable identity, source system, version and capability
/// surface, as the catalog exposes it. Derived from a <see cref="ConnectorDescriptor"/>.
/// </summary>
/// <param name="Key">The connector key.</param>
/// <param name="Name">The human-readable name.</param>
/// <param name="SourceSystem">The source system the connector reads or targets.</param>
/// <param name="Version">The connector version.</param>
/// <param name="Description">An optional description.</param>
/// <param name="Capabilities">The declared capability flags.</param>
/// <param name="Provides">The Standard Model entity types the connector produces.</param>
public sealed record ConnectorMetadata(
    string Key,
    string Name,
    string SourceSystem,
    ConnectorVersion Version,
    string? Description,
    ConnectorCapability Capabilities,
    IReadOnlyList<string> Provides)
{
    /// <summary>Projects a descriptor into its metadata.</summary>
    /// <param name="descriptor">The descriptor to project.</param>
    /// <returns>The metadata.</returns>
    public static ConnectorMetadata FromDescriptor(ConnectorDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        var manifest = descriptor.Manifest;
        return new ConnectorMetadata(
            manifest.Key,
            manifest.Name,
            manifest.SourceSystem,
            descriptor.Version,
            manifest.Description,
            descriptor.Capabilities,
            manifest.Provides);
    }
}
