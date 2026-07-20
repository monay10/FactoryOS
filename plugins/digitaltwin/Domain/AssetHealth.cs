namespace FactoryOS.Plugins.DigitalTwin.Domain;

/// <summary>The asset's latest OEE health, when one has been observed.</summary>
/// <param name="Oee">The latest Overall Equipment Effectiveness, a fraction in <c>[0, 1]</c>.</param>
/// <param name="MeetsTarget">Whether that OEE met the asset's target.</param>
/// <param name="At">The end of the period the OEE was computed for.</param>
public readonly record struct AssetHealth(decimal Oee, bool MeetsTarget, DateTimeOffset At);
