namespace FactoryOS.Plugins.Hr;

/// <summary>
/// Configuration for the HR module. Behaviour varies by configuration, never by customer branch: a factory
/// decides whether a worker who has no record of a required certification counts as a gap.
/// </summary>
public sealed record HrOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Modules:Hr";

    /// <summary>
    /// When <see langword="true"/> (default), staffing a worker with no record of the required certification is
    /// a <c>Missing</c> gap. When <see langword="false"/>, only a recorded-but-expired certification is a gap.
    /// </summary>
    public bool TreatMissingAsGap { get; init; } = true;
}
