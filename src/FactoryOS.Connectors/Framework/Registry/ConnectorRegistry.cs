using System.Collections.Concurrent;
using FactoryOS.Connectors.Framework.Runtime;

namespace FactoryOS.Connectors.Framework.Registry;

/// <summary>
/// The authoritative, thread-safe catalogue of known connectors. The registry tracks each connector's
/// descriptor and mediates enable/disable requests.
/// </summary>
public interface IConnectorRegistry
{
    /// <summary>Gets a snapshot of all registered connector descriptors.</summary>
    IReadOnlyCollection<ConnectorDescriptor> All { get; }

    /// <summary>Finds a descriptor by connector key.</summary>
    /// <param name="key">The connector key.</param>
    /// <returns>The descriptor, or <see langword="null"/> when no connector with that key is registered.</returns>
    ConnectorDescriptor? Find(string key);

    /// <summary>Registers a descriptor, replacing any existing entry with the same key.</summary>
    /// <param name="descriptor">The descriptor to register.</param>
    void Register(ConnectorDescriptor descriptor);

    /// <summary>Enables a connector so the host will manage it.</summary>
    /// <param name="key">The connector key.</param>
    /// <returns><see langword="true"/> when a connector with that key exists; otherwise <see langword="false"/>.</returns>
    bool Enable(string key);

    /// <summary>Disables a connector so the host will skip it.</summary>
    /// <param name="key">The connector key.</param>
    /// <returns><see langword="true"/> when a connector with that key exists; otherwise <see langword="false"/>.</returns>
    bool Disable(string key);
}

/// <summary>Default in-memory, thread-safe <see cref="IConnectorRegistry"/>.</summary>
public sealed class ConnectorRegistry : IConnectorRegistry
{
    private readonly ConcurrentDictionary<string, ConnectorDescriptor> _descriptors =
        new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public IReadOnlyCollection<ConnectorDescriptor> All => _descriptors.Values.ToArray();

    /// <inheritdoc />
    public ConnectorDescriptor? Find(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _descriptors.TryGetValue(key, out var descriptor) ? descriptor : null;
    }

    /// <inheritdoc />
    public void Register(ConnectorDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        _descriptors[descriptor.Key] = descriptor;
    }

    /// <inheritdoc />
    public bool Enable(string key)
    {
        var descriptor = Find(key);
        if (descriptor is null)
        {
            return false;
        }

        if (descriptor.State == ConnectorState.Disabled)
        {
            descriptor.MarkDiscovered();
        }

        return true;
    }

    /// <inheritdoc />
    public bool Disable(string key)
    {
        var descriptor = Find(key);
        if (descriptor is null)
        {
            return false;
        }

        descriptor.MarkDisabled();
        return true;
    }
}
