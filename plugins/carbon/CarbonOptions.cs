namespace FactoryOS.Plugins.Carbon;

/// <summary>
/// Configuration for the Carbon module. Behaviour varies by configuration, never by customer branch: a factory
/// supplies its emission factors as data (kg CO₂e per energy unit, per metric) — the mapping is a manifest, not
/// code.
/// </summary>
public sealed record CarbonOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Modules:Carbon";

    /// <summary>
    /// Emission factors in kg CO₂e per energy unit, keyed by the energy metric (for example
    /// <c>ActivePower</c>). A metric without an entry falls back to <see cref="DefaultEmissionFactor"/>.
    /// </summary>
    public IReadOnlyDictionary<string, decimal> EmissionFactors { get; init; } =
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The emission factor applied to metrics with no explicit entry. Non-positive (the default) means such
    /// metrics produce no emission and are not reported.
    /// </summary>
    public decimal DefaultEmissionFactor { get; init; }
}
