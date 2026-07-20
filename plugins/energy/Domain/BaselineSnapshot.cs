namespace FactoryOS.Plugins.Energy.Domain;

/// <summary>
/// The state of a meter's baseline <b>before</b> the current reading was folded in: how many prior readings
/// there were and their average. Spike detection compares the incoming value against this prior baseline.
/// </summary>
/// <param name="PriorCount">The number of readings seen before the current one.</param>
/// <param name="PriorAverage">The average of those prior readings (<c>0</c> when there were none).</param>
public readonly record struct BaselineSnapshot(int PriorCount, decimal PriorAverage);
