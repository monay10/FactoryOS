using FactoryOS.Plugins.Quality;
using FactoryOS.Plugins.Quality.Domain;

namespace FactoryOS.Tests.Quality;

public sealed class DefectRateEvaluatorTests
{
    private static readonly QualityOptions Options = new()
    {
        DefectRateThreshold = 0.05m,
        MinimumInspectedUnits = 20,
        WindowSize = 20,
    };

    [Fact]
    public void Stays_inert_until_enough_units_are_inspected()
    {
        // 5 defects in 10 units = 50% rate, but below the 20-unit evidence floor.
        var evaluation = DefectRateEvaluator.Evaluate(new DefectRateSnapshot(10, 5), Options);

        Assert.False(evaluation.IsBreach);
        Assert.Equal(0.5m, evaluation.DefectRate);
    }

    [Fact]
    public void Breaches_when_rate_exceeds_threshold_with_enough_evidence()
    {
        // 6 defects in 100 units = 6% > 5%.
        var evaluation = DefectRateEvaluator.Evaluate(new DefectRateSnapshot(100, 6), Options);

        Assert.True(evaluation.IsBreach);
        Assert.Equal(0.06m, evaluation.DefectRate);
    }

    [Fact]
    public void Does_not_breach_at_or_below_threshold()
    {
        // 5 defects in 100 units = exactly 5% — not a breach (strictly greater required).
        var evaluation = DefectRateEvaluator.Evaluate(new DefectRateSnapshot(100, 5), Options);

        Assert.False(evaluation.IsBreach);
        Assert.Equal(0.05m, evaluation.DefectRate);
    }

    [Fact]
    public void An_empty_window_has_a_zero_rate_and_no_breach()
    {
        var evaluation = DefectRateEvaluator.Evaluate(default, Options);

        Assert.False(evaluation.IsBreach);
        Assert.Equal(0m, evaluation.DefectRate);
    }
}
