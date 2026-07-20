using System.Collections.Concurrent;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Plugin.Runtime;

namespace FactoryOS.Plugin.Registry;

/// <summary>Default in-memory, thread-safe <see cref="IPluginRegistry"/>.</summary>
public sealed class PluginRegistry : IPluginRegistry
{
    private readonly ConcurrentDictionary<string, PluginDescriptor> _descriptors =
        new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public IReadOnlyCollection<PluginDescriptor> All => _descriptors.Values.ToArray();

    /// <inheritdoc />
    public PluginDescriptor? Find(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _descriptors.TryGetValue(key, out var descriptor) ? descriptor : null;
    }

    /// <inheritdoc />
    public void Register(PluginDescriptor descriptor)
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

        if (descriptor.State == PluginState.Disabled)
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
