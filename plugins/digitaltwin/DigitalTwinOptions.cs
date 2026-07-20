namespace FactoryOS.Plugins.DigitalTwin;

/// <summary>
/// Configuration for the Digital Twin read-model. Behaviour varies by configuration, never by customer branch:
/// a factory tunes the OEE fraction below which an asset's health reads as degraded.
/// </summary>
public sealed record DigitalTwinOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Modules:DigitalTwin";

    /// <summary>
    /// The OEE fraction (in <c>[0, 1]</c>) at or below which an asset's status is reported as degraded, when the
    /// latest OEE reading also missed its target. Purely a display threshold for the twin.
    /// </summary>
    public decimal DegradedOeeThreshold { get; init; } = 0.60m;
}
