namespace FactoryOS.Plugins.Reporting;

/// <summary>
/// Configuration for the Reporting read-model. Behaviour varies by configuration, never by customer branch: a
/// factory decides how many days of daily OEE history each machine retains.
/// </summary>
public sealed record ReportingOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Modules:Reporting";

    /// <summary>
    /// The maximum number of most-recent day buckets retained per machine. Older days fall off once the cap is
    /// reached. Must be at least one.
    /// </summary>
    public int RetainDays { get; init; } = 90;

    /// <summary>The scheduled-task action that triggers OEE report generation (matched case-insensitively).</summary>
    public string ReportAction { get; init; } = "GenerateReport";

    /// <summary>The object-store key prefix under which rendered OEE reports are stored.</summary>
    public string ReportKeyPrefix { get; init; } = "reports/oee/";
}
