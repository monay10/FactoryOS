namespace FactoryOS.Plugins.Safety;

/// <summary>
/// Configuration for the Safety module. Behaviour varies by configuration, never by customer branch: a factory
/// sets the severity that warrants an immediate stand-down, how many recent incidents at a site trigger one on
/// frequency alone, and how long the rolling window is.
/// </summary>
public sealed record SafetyOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Modules:Safety";

    /// <summary>The severity (1–5) at or above which a single incident triggers a stand-down on its own.</summary>
    public int StandDownSeverity { get; init; } = 4;

    /// <summary>The number of incidents within the window at a site that triggers a stand-down on frequency alone.</summary>
    public int FrequencyThreshold { get; init; } = 3;

    /// <summary>How many recent incidents the frequency count is measured over. Must be positive.</summary>
    public int WindowSize { get; init; } = 10;
}
