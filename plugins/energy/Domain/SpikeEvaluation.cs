namespace FactoryOS.Plugins.Energy.Domain;

/// <summary>The outcome of evaluating a reading against its baseline.</summary>
/// <param name="IsSpike">Whether the reading exceeds the baseline by at least the configured threshold.</param>
/// <param name="Baseline">The baseline the reading was compared against.</param>
/// <param name="DeltaPercent">How far above the baseline the reading is, in percent (may be negative).</param>
public readonly record struct SpikeEvaluation(bool IsSpike, decimal Baseline, decimal DeltaPercent);
