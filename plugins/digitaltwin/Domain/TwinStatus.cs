namespace FactoryOS.Plugins.DigitalTwin.Domain;

/// <summary>
/// The derived status labels a twin reports. Computed from the asset's latest state, never stored — so the twin
/// is always a pure reflection of the events that shaped it.
/// </summary>
public static class TwinStatus
{
    /// <summary>The asset is reporting and healthy.</summary>
    public const string Online = "Online";

    /// <summary>The asset is reporting but its latest OEE missed target and sits at or below the threshold.</summary>
    public const string Degraded = "Degraded";

    /// <summary>Nothing has been observed for the asset yet.</summary>
    public const string Unknown = "Unknown";
}
