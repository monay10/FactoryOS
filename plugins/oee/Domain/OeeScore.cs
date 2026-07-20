namespace FactoryOS.Plugins.Oee.Domain;

/// <summary>The three OEE factors and their product, each a fraction in <c>[0, 1]</c>.</summary>
/// <param name="Availability">Run time / planned time.</param>
/// <param name="Performance">(Ideal cycle time × total count) / run time, clamped to <c>[0, 1]</c>.</param>
/// <param name="Quality">Good count / total count.</param>
/// <param name="Oee">Availability × Performance × Quality.</param>
public readonly record struct OeeScore(decimal Availability, decimal Performance, decimal Quality, decimal Oee);
