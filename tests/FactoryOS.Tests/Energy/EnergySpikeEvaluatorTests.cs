using FactoryOS.Plugins.Energy;
using FactoryOS.Plugins.Energy.Domain;

namespace FactoryOS.Tests.Energy;

public sealed class EnergySpikeEvaluatorTests
{
    private static readonly EnergyOptions Options = new() { SpikeThreshold = 0.25m, MinimumSamples = 3 };

    [Fact]
    public void No_spike_before_the_minimum_sample_count()
    {
        var evaluation = EnergySpikeEvaluator.Evaluate(1000m, new BaselineSnapshot(2, 100m), Options);

        Assert.False(evaluation.IsSpike); // only 2 prior samples < 3
    }

    [Fact]
    public void No_spike_when_the_baseline_is_non_positive()
    {
        var evaluation = EnergySpikeEvaluator.Evaluate(1000m, new BaselineSnapshot(5, 0m), Options);

        Assert.False(evaluation.IsSpike);
    }

    [Fact]
    public void No_spike_within_the_threshold()
    {
        var evaluation = EnergySpikeEvaluator.Evaluate(120m, new BaselineSnapshot(5, 100m), Options);

        Assert.False(evaluation.IsSpike); // +20% < 25%
        Assert.Equal(20m, evaluation.DeltaPercent);
    }

    [Fact]
    public void Spike_at_or_above_the_threshold()
    {
        var evaluation = EnergySpikeEvaluator.Evaluate(200m, new BaselineSnapshot(5, 100m), Options);

        Assert.True(evaluation.IsSpike);
        Assert.Equal(100m, evaluation.Baseline);
        Assert.Equal(100m, evaluation.DeltaPercent); // +100%
    }

    [Fact]
    public void A_drop_is_never_a_spike()
    {
        var evaluation = EnergySpikeEvaluator.Evaluate(50m, new BaselineSnapshot(5, 100m), Options);

        Assert.False(evaluation.IsSpike);
        Assert.Equal(-50m, evaluation.DeltaPercent);
    }
}
