namespace FactoryOS.Plugins.Workflow.Monitoring.Domain;

/// <summary>
/// Which part of the platform a metric describes. The categories mirror the platform's own layering, so a
/// single metric store can be sliced back into the collections it was gathered from.
/// </summary>
public enum MetricCategory
{
    /// <summary>The workflow runtime.</summary>
    Workflow = 0,

    /// <summary>The forms engine.</summary>
    Form = 1,

    /// <summary>The human task engine.</summary>
    HumanTask = 2,

    /// <summary>The approval engine.</summary>
    Approval = 3,

    /// <summary>The notification engine.</summary>
    Notification = 4,

    /// <summary>The SLA engine.</summary>
    Sla = 5,

    /// <summary>The audit engine.</summary>
    Audit = 6,

    /// <summary>Connectors reaching outside systems.</summary>
    Connector = 7,

    /// <summary>Plugin lifecycle and load.</summary>
    Plugin = 8,

    /// <summary>Inbound API traffic.</summary>
    Api = 9,

    /// <summary>Infrastructure the platform runs on — database, cache, storage, broker.</summary>
    Infrastructure = 10,

    /// <summary>The process the platform runs in.</summary>
    Runtime = 11,

    /// <summary>Cross-cutting performance measurements.</summary>
    Performance = 12,
}

/// <summary>
/// What kind of quantity a metric carries. This is not decoration: it decides how a metric may be aggregated
/// and, crucially, what sampling is allowed to do to it — dropping one gauge reading loses nothing, while
/// dropping one counter increment silently understates a total.
/// </summary>
public enum MetricKind
{
    /// <summary>A monotonically increasing count of occurrences.</summary>
    Counter = 0,

    /// <summary>A value that rises and falls, meaningful only at the instant it was read.</summary>
    Gauge = 1,

    /// <summary>An elapsed time, in milliseconds.</summary>
    Duration = 2,
}

/// <summary>How a set of metric values collapses into a single number.</summary>
public enum MetricAggregation
{
    /// <summary>The total of every value.</summary>
    Sum = 0,

    /// <summary>How many values there are.</summary>
    Count = 1,

    /// <summary>The arithmetic mean.</summary>
    Average = 2,

    /// <summary>The smallest value.</summary>
    Minimum = 3,

    /// <summary>The largest value.</summary>
    Maximum = 4,

    /// <summary>The most recent value.</summary>
    Last = 5,

    /// <summary>The total divided by the window length, per second.</summary>
    Rate = 6,

    /// <summary>The 95th percentile, for latency where the mean hides the tail.</summary>
    Percentile95 = 7,
}

/// <summary>How a measured value is compared against a threshold.</summary>
public enum MetricComparison
{
    /// <summary>The value must stay at or below the limit.</summary>
    GreaterThan = 0,

    /// <summary>The value must stay at or above the limit.</summary>
    LessThan = 1,

    /// <summary>The value must stay below the limit.</summary>
    GreaterThanOrEqual = 2,

    /// <summary>The value must stay above the limit.</summary>
    LessThanOrEqual = 3,
}

/// <summary>The state a threshold assigns to a metric it evaluated.</summary>
public enum MetricHealthState
{
    /// <summary>No value has been measured in the window, so nothing can be said.</summary>
    Unknown = 0,

    /// <summary>The metric is within its threshold.</summary>
    Ok = 1,

    /// <summary>The metric crossed its warning limit.</summary>
    Warning = 2,

    /// <summary>The metric crossed its critical limit.</summary>
    Critical = 3,
}

/// <summary>The status a health check reports for the component it probes.</summary>
public enum HealthStatus
{
    /// <summary>The component produced no signal in the window, so its state is not known.</summary>
    Unknown = 0,

    /// <summary>The component is working.</summary>
    Healthy = 1,

    /// <summary>The component is working, but worse than it should.</summary>
    Degraded = 2,

    /// <summary>The component is failing.</summary>
    Unhealthy = 3,
}

/// <summary>What retention does with values that outlive their raw window.</summary>
public enum MetricRetentionAction
{
    /// <summary>The values are removed.</summary>
    Delete = 0,

    /// <summary>The values are collapsed into one aggregated value per bucket, then removed.</summary>
    RollUp = 1,
}

/// <summary>The rights that can be granted over the monitoring surface.</summary>
[Flags]
public enum MonitoringPermission
{
    /// <summary>No rights.</summary>
    None = 0,

    /// <summary>Read metric series, snapshots and searches.</summary>
    ViewMetrics = 1,

    /// <summary>Read health check results and the overall health report.</summary>
    ViewHealth = 2,

    /// <summary>Register and change thresholds and alert rules.</summary>
    ManageThresholds = 4,
}
