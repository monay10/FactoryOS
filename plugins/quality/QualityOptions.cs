namespace FactoryOS.Plugins.Quality;

/// <summary>
/// Configuration for the Quality module. Behaviour varies by configuration, never by customer branch: a factory
/// sets the defect-rate it will tolerate, how much evidence must accumulate before an alert can fire, and how
/// long the rolling window is.
/// </summary>
public sealed record QualityOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Modules:Quality";

    /// <summary>The rolling defect rate that raises an alert once exceeded, as a fraction in <c>[0, 1]</c>.</summary>
    public decimal DefectRateThreshold { get; init; } = 0.05m;

    /// <summary>How many units must be inspected within the window before an alert can fire, avoiding cold-start noise.</summary>
    public int MinimumInspectedUnits { get; init; } = 20;

    /// <summary>How many recent inspections the rolling defect rate is computed over. Must be positive.</summary>
    public int WindowSize { get; init; } = 20;
}
