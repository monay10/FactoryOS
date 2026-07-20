namespace FactoryOS.Plugins.Oee.Domain;

/// <summary>
/// Computes the OEE factors. Pure and deterministic — no state, no I/O — so it is fully offline-testable. Every
/// factor guards its denominator (a non-positive denominator yields <c>0</c> rather than throwing) and is
/// clamped to <c>[0, 1]</c>, so a mis-configured ideal cycle time can never report Performance above 100%.
/// </summary>
public static class OeeCalculator
{
    /// <summary>Computes Availability, Performance, Quality and their product from period inputs.</summary>
    /// <param name="plannedTimeSeconds">Planned production time (Availability denominator).</param>
    /// <param name="runTimeSeconds">Actual running time (Availability numerator, Performance denominator).</param>
    /// <param name="idealCycleTimeSeconds">Ideal seconds per unit (Performance basis).</param>
    /// <param name="totalCount">Total units produced.</param>
    /// <param name="goodCount">Good units produced (Quality numerator).</param>
    /// <returns>The clamped OEE score.</returns>
    public static OeeScore Calculate(
        decimal plannedTimeSeconds,
        decimal runTimeSeconds,
        decimal idealCycleTimeSeconds,
        int totalCount,
        int goodCount)
    {
        var availability = Ratio(runTimeSeconds, plannedTimeSeconds);
        var performance = Ratio(idealCycleTimeSeconds * totalCount, runTimeSeconds);
        var quality = Ratio(goodCount, totalCount);
        var oee = availability * performance * quality;

        return new OeeScore(availability, performance, quality, oee);
    }

    private static decimal Ratio(decimal numerator, decimal denominator)
    {
        if (denominator <= 0m)
        {
            return 0m;
        }

        return Math.Clamp(numerator / denominator, 0m, 1m);
    }
}
