namespace FactoryOS.Plugins.Dashboard;

/// <summary>
/// Configuration for the Dashboard read-model. Behaviour varies by configuration, never by customer branch:
/// a factory tunes how deep the live alert feed runs, nothing more.
/// </summary>
public sealed record DashboardOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Modules:Dashboard";

    /// <summary>
    /// The maximum number of most-recent alerts retained per tenant in the live feed. Older alerts fall off the
    /// tail once the cap is reached. Must be at least one.
    /// </summary>
    public int RecentAlertCapacity { get; init; } = 50;
}
