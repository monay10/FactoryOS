using System.Collections.Concurrent;
using FactoryOS.Contracts.Iot;

namespace FactoryOS.Iot.Registry;

/// <summary>Default in-memory, thread-safe <see cref="IDeviceRegistry"/>, keyed by tenant and device id.</summary>
public sealed class InMemoryDeviceRegistry : IDeviceRegistry
{
    // A unit-separator delimiter keeps the composite key unambiguous across the tenant/device boundary.
    private const char KeySeparator = '';

    private readonly ConcurrentDictionary<string, Device> _devices = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void Register(Device device)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentException.ThrowIfNullOrWhiteSpace(device.Tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(device.DeviceId);
        _devices[Key(device.Tenant, device.DeviceId)] = device;
    }

    /// <inheritdoc />
    public Device? Find(string tenant, string deviceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        return _devices.TryGetValue(Key(tenant, deviceId), out var device) ? device : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<Device> ForTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return _devices.Values
            .Where(device => string.Equals(device.Tenant, tenant, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static string Key(string tenant, string deviceId) => string.Join(KeySeparator, tenant, deviceId);
}
