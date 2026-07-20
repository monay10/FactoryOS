namespace FactoryOS.Contracts.Iot;

/// <summary>
/// A registered device (a PLC, gateway or sensor) and its tag catalogue. Devices are tenant-scoped; a
/// device's tags define how its raw telemetry channels normalize into Standard Model meter readings.
/// </summary>
public sealed record Device
{
    /// <summary>Gets the tenant the device belongs to.</summary>
    public required string Tenant { get; init; }

    /// <summary>Gets the stable, tenant-unique device identifier.</summary>
    public required string DeviceId { get; init; }

    /// <summary>Gets the human-readable device name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the device type or model (for example <c>Modbus-PM</c>).</summary>
    public string DeviceType { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the device is enabled for ingestion.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Gets the tag catalogue describing the device's channels.</summary>
    public IReadOnlyList<DeviceTag> Tags { get; init; } = [];

    /// <summary>Finds a tag by its raw channel name, or <see langword="null"/> when absent.</summary>
    /// <param name="tagName">The raw channel name.</param>
    /// <returns>The matching <see cref="DeviceTag"/>, or <see langword="null"/>.</returns>
    public DeviceTag? FindTag(string tagName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tagName);

        foreach (var tag in Tags)
        {
            if (string.Equals(tag.Name, tagName, StringComparison.OrdinalIgnoreCase))
            {
                return tag;
            }
        }

        return null;
    }
}
