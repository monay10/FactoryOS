using FactoryOS.Plugins.Oee.Domain;

namespace FactoryOS.Tests.Oee;

public sealed class OeeCalculatorTests
{
    [Fact]
    public void Computes_availability_from_run_over_planned_time()
    {
        var score = OeeCalculator.Calculate(plannedTimeSeconds: 100m, runTimeSeconds: 75m, idealCycleTimeSeconds: 1m, totalCount: 75, goodCount: 75);

        Assert.Equal(0.75m, score.Availability);
        Assert.Equal(1m, score.Performance);
        Assert.Equal(1m, score.Quality);
        Assert.Equal(0.75m, score.Oee);
    }

    [Fact]
    public void Clamps_performance_to_one_when_faster_than_ideal()
    {
        var score = OeeCalculator.Calculate(plannedTimeSeconds: 100m, runTimeSeconds: 100m, idealCycleTimeSeconds: 2m, totalCount: 100, goodCount: 100);

        Assert.Equal(1m, score.Performance); // (2×100)/100 = 2.0 → clamped
    }

    [Fact]
    public void Computes_quality_from_good_over_total()
    {
        var score = OeeCalculator.Calculate(plannedTimeSeconds: 100m, runTimeSeconds: 100m, idealCycleTimeSeconds: 1m, totalCount: 100, goodCount: 80);

        Assert.Equal(0.8m, score.Quality);
        Assert.Equal(0.8m, score.Oee);
    }

    [Fact]
    public void Combines_the_three_factors()
    {
        // A = 90/100 = 0.9; P = (1×72)/90 = 0.8; Q = 54/72 = 0.75; OEE = 0.54
        var score = OeeCalculator.Calculate(plannedTimeSeconds: 100m, runTimeSeconds: 90m, idealCycleTimeSeconds: 1m, totalCount: 72, goodCount: 54);

        Assert.Equal(0.9m, score.Availability);
        Assert.Equal(0.8m, score.Performance);
        Assert.Equal(0.75m, score.Quality);
        Assert.Equal(0.54m, score.Oee);
    }

    [Fact]
    public void Non_positive_denominators_yield_zero_not_an_exception()
    {
        var score = OeeCalculator.Calculate(plannedTimeSeconds: 0m, runTimeSeconds: 0m, idealCycleTimeSeconds: 1m, totalCount: 0, goodCount: 0);

        Assert.Equal(0m, score.Availability);
        Assert.Equal(0m, score.Performance);
        Assert.Equal(0m, score.Quality);
        Assert.Equal(0m, score.Oee);
    }
}
