namespace FactoryOS.Plugins.Workflow.Monitoring.Configuration;

/// <summary>Stable constants for the monitoring engine.</summary>
public static class MonitoringConstants
{
    /// <summary>The configuration section the engine options bind from.</summary>
    public const string ConfigurationSection = "Workflow:Monitoring";

    /// <summary>The tenant used when a source event carries none.</summary>
    public const string DefaultTenant = "default";

    /// <summary>The culture used when a metric or health message declares no localization.</summary>
    public const string DefaultCulture = "en";

    /// <summary>The dimension label carrying the definition or entity key a measurement belongs to.</summary>
    public const string KeyLabel = "key";

    /// <summary>The dimension label carrying the outcome of the measured operation.</summary>
    public const string OutcomeLabel = "outcome";

    /// <summary>The dimension label carrying the channel a notification measurement belongs to.</summary>
    public const string ChannelLabel = "channel";

    /// <summary>
    /// The key a measurement is filed under when the event that produced it did not carry one — for work whose
    /// start this process never saw. Saying so plainly is better than guessing an identifier that would split
    /// one series into thousands.
    /// </summary>
    public const string UnknownKey = "unknown";
}

/// <summary>
/// Runtime options for the monitoring engine (namespace <c>Monitoring.Configuration</c>). These govern the
/// monitoring runtime only; they are independent of every engine whose events it observes, and none of those
/// engines is modified.
/// </summary>
public sealed record MonitoringEngineOptions
{
    /// <summary>
    /// Gets a value indicating whether a <c>MetricCollected</c> event is published for every admitted sample.
    /// Off by default, and deliberately so: the observability layer sees every event every engine raises, and
    /// re-publishing each one would make monitoring by far the loudest producer on the bus. Turn it on when
    /// something downstream genuinely needs the raw stream — an exporter, or a live wall dashboard.
    /// </summary>
    public bool PublishCollectionEvents { get; init; }

    /// <summary>
    /// Gets the window aggregations and threshold evaluations cover when a threshold declares none.
    /// </summary>
    public TimeSpan DefaultWindow { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets how long raw values are kept when no retention policy matches. A metric store with no ceiling is a
    /// memory leak with a dashboard attached, so there is always a default.
    /// </summary>
    public TimeSpan DefaultRetention { get; init; } = TimeSpan.FromDays(7);

    /// <summary>Gets the maximum number of values a single retention pass removes or rolls up.</summary>
    public int MaintenanceBatchSize { get; init; } = 5000;

    /// <summary>Gets how long a health probe may run before it is reported as unhealthy.</summary>
    public TimeSpan HealthCheckTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets the window a health check reads its signals over — error ratios, latency and liveness are all
    /// judged across it.
    /// </summary>
    public TimeSpan HealthWindow { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets the default culture used when a metric or health message declares no localization.</summary>
    public string DefaultCulture { get; init; } = MonitoringConstants.DefaultCulture;
}
