namespace FactoryOS.Plugins.Carbon.Domain;

/// <summary>
/// Resolves the emission factor for an energy metric from configuration. Pure and deterministic — no state, no
/// I/O — so it is fully offline-testable. An explicit per-metric factor wins; otherwise the configured default
/// applies. The mapping is data, never a branch on the metric name.
/// </summary>
public static class EmissionFactorResolver
{
    /// <summary>Resolves the emission factor for a metric.</summary>
    /// <param name="metric">The energy metric.</param>
    /// <param name="options">The emission-factor configuration.</param>
    /// <returns>The factor in kg CO₂e per energy unit; the default when the metric has no explicit entry.</returns>
    public static decimal Resolve(string metric, CarbonOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metric);
        ArgumentNullException.ThrowIfNull(options);

        return options.EmissionFactors.TryGetValue(metric, out var factor)
            ? factor
            : options.DefaultEmissionFactor;
    }
}
