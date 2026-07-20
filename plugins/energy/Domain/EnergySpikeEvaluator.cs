namespace FactoryOS.Plugins.Energy.Domain;

/// <summary>
/// Decides whether a reading is a consumption spike relative to its prior baseline. Pure and deterministic —
/// no state, no I/O — so it is fully offline-testable. Detection stays inert until enough history exists and
/// when the baseline is non-positive, so a cold start never fires false spikes.
/// </summary>
public static class EnergySpikeEvaluator
{
    /// <summary>Evaluates a reading against its prior baseline under the given options.</summary>
    /// <param name="value">The incoming reading value.</param>
    /// <param name="baseline">The baseline as it stood before this reading.</param>
    /// <param name="options">The spike threshold and minimum-sample settings.</param>
    /// <returns>The evaluation, including the percentage delta from baseline.</returns>
    public static SpikeEvaluation Evaluate(decimal value, BaselineSnapshot baseline, EnergyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (baseline.PriorCount < options.MinimumSamples || baseline.PriorAverage <= 0m)
        {
            return new SpikeEvaluation(false, baseline.PriorAverage, 0m);
        }

        var delta = (value - baseline.PriorAverage) / baseline.PriorAverage;
        var isSpike = delta >= options.SpikeThreshold;
        return new SpikeEvaluation(isSpike, baseline.PriorAverage, delta * 100m);
    }
}
