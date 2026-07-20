namespace FactoryOS.Plugins.Energy;

/// <summary>
/// Configuration for the Energy module. Behaviour varies by configuration, never by customer branch: a
/// factory tunes spike sensitivity and baseline history here.
/// </summary>
public sealed record EnergyOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Modules:Energy";

    /// <summary>
    /// The fractional increase over the rolling baseline that counts as a spike (for example <c>0.25</c> =
    /// 25% above baseline).
    /// </summary>
    public decimal SpikeThreshold { get; init; } = 0.25m;

    /// <summary>The minimum number of prior readings required before spike detection activates.</summary>
    public int MinimumSamples { get; init; } = 3;

    /// <summary>How many recent readings the rolling baseline averages over, per meter and metric.</summary>
    public int BaselineWindow { get; init; } = 20;

    /// <summary>How many recent spikes the read model retains per tenant for the dashboard feed.</summary>
    public int SpikeFeedCapacity { get; init; } = 50;
}
