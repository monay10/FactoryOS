using FactoryOS.Contracts.Iot;

namespace FactoryOS.Iot.Registry;

/// <summary>
/// The tenant-scoped catalogue of registered devices. Telemetry can only be normalized for a device that
/// is registered, so the registry is the authority on which devices — and tags — the hub accepts.
/// </summary>
public interface IDeviceRegistry
{
    /// <summary>Registers or replaces a device.</summary>
    /// <param name="device">The device to register.</param>
    void Register(Device device);

    /// <summary>Finds a device by tenant and identifier.</summary>
    /// <param name="tenant">The tenant that owns the device.</param>
    /// <param name="deviceId">The device identifier.</param>
    /// <returns>The device, or <see langword="null"/> when it is not registered.</returns>
    Device? Find(string tenant, string deviceId);

    /// <summary>Lists all devices registered for a tenant.</summary>
    /// <param name="tenant">The tenant to list devices for.</param>
    /// <returns>The tenant's registered devices.</returns>
    IReadOnlyList<Device> ForTenant(string tenant);
}
