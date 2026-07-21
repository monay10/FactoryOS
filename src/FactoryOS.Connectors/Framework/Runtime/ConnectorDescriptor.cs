using FactoryOS.Contracts.Connectors;

namespace FactoryOS.Connectors.Framework.Runtime;

/// <summary>The lifecycle state of a connector as tracked by the platform.</summary>
public enum ConnectorState
{
    /// <summary>The connector's manifest has been discovered but it is not yet initialized.</summary>
    Discovered = 0,

    /// <summary>The connector is intentionally switched off and will be skipped by the host.</summary>
    Disabled = 1,

    /// <summary>The connector instance has been attached and initialized.</summary>
    Initialized = 2,

    /// <summary>The connector is connected to its source system.</summary>
    Connected = 3,

    /// <summary>The connector has been disconnected from its source system.</summary>
    Disconnected = 4,

    /// <summary>The connector failed to initialize, connect or operate; see the recorded reason.</summary>
    Faulted = 5,
}

/// <summary>
/// The platform's runtime view of a connector: its manifest, declared version and capabilities, mutable
/// lifecycle state, the resolved instance and, on failure, the reason. Descriptors are the registry's unit
/// of record. The manifest is reused from the connector contracts; the version and capabilities are
/// declared at registration since the contract manifest does not carry them.
/// </summary>
public sealed class ConnectorDescriptor
{
    /// <summary>Initializes a new instance of the <see cref="ConnectorDescriptor"/> class.</summary>
    /// <param name="manifest">The connector manifest.</param>
    /// <param name="version">The connector version (defaults to <c>1.0.0</c>).</param>
    /// <param name="capabilities">The declared capabilities (defaults to <see cref="ConnectorCapability.Read"/>).</param>
    /// <param name="location">The directory the connector was discovered in, if any.</param>
    public ConnectorDescriptor(
        ConnectorManifest manifest,
        ConnectorVersion version = default,
        ConnectorCapability capabilities = ConnectorCapability.Read,
        string? location = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        Manifest = manifest;
        Version = version == default ? new ConnectorVersion(1, 0, 0) : version;
        Capabilities = capabilities;
        Location = location;
        State = ConnectorState.Discovered;
    }

    /// <summary>Gets the connector manifest.</summary>
    public ConnectorManifest Manifest { get; }

    /// <summary>Gets the connector key (a shortcut for <c>Manifest.Key</c>).</summary>
    public string Key => Manifest.Key;

    /// <summary>Gets the connector version.</summary>
    public ConnectorVersion Version { get; }

    /// <summary>Gets the declared capabilities.</summary>
    public ConnectorCapability Capabilities { get; }

    /// <summary>Gets the directory the connector was discovered in, if any.</summary>
    public string? Location { get; }

    /// <summary>Gets the current lifecycle state.</summary>
    public ConnectorState State { get; private set; }

    /// <summary>Gets the resolved connector instance, once attached.</summary>
    public IConnector? Instance { get; private set; }

    /// <summary>Gets the failure reason when <see cref="State"/> is <see cref="ConnectorState.Faulted"/>.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>Determines whether the connector declares a capability.</summary>
    /// <param name="capability">The capability to test for.</param>
    /// <returns><see langword="true"/> when the capability is declared.</returns>
    public bool Supports(ConnectorCapability capability) => Capabilities.Supports(capability);

    /// <summary>Attaches the connector instance.</summary>
    /// <param name="instance">The connector instance.</param>
    public void AttachInstance(IConnector instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        Instance = instance;
        FailureReason = null;
    }

    /// <summary>Marks the connector as initialized.</summary>
    public void MarkInitialized() => State = ConnectorState.Initialized;

    /// <summary>Marks the connector as connected.</summary>
    public void MarkConnected() => State = ConnectorState.Connected;

    /// <summary>Marks the connector as disconnected.</summary>
    public void MarkDisconnected() => State = ConnectorState.Disconnected;

    /// <summary>Marks the connector as disabled, so the host skips it.</summary>
    public void MarkDisabled() => State = ConnectorState.Disabled;

    /// <summary>Returns the connector to the discovered state (for example after disposal).</summary>
    public void MarkDiscovered() => State = ConnectorState.Discovered;

    /// <summary>Marks the connector as faulted and records why.</summary>
    /// <param name="reason">A human-readable failure reason.</param>
    public void MarkFaulted(string reason)
    {
        FailureReason = reason;
        State = ConnectorState.Faulted;
    }
}
