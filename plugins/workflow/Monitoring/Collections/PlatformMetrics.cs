using FactoryOS.Plugins.Workflow.Monitoring.Domain;

namespace FactoryOS.Plugins.Workflow.Monitoring.Collections;

/// <summary>
/// What the connector layer is measured by — every call the platform makes to a system outside itself.
/// <para>
/// These metrics have no event seam of their own: connectors are reached through the connector framework, and
/// whatever calls a connector records the outcome. That is the same consumer relationship the engine bridges
/// have, expressed as a call rather than a subscription, and it keeps monitoring on the reading end of the
/// arrow either way.
/// </para>
/// </summary>
public static class ConnectorMetricCollection
{
    /// <summary>Calls made through a connector.</summary>
    public const string Calls = "connector.call";

    /// <summary>Calls that came back as a failure.</summary>
    public const string Failures = "connector.failed";

    /// <summary>How long a connector call took.</summary>
    public const string Latency = "connector.latency";

    /// <summary>Gets the definitions in this collection.</summary>
    public static IReadOnlyList<MetricDefinition> Definitions { get; } =
    [
        Metric.Counter(Calls, MetricCategory.Connector, "Calls made through a connector.", "key", "outcome"),
        Metric.Counter(Failures, MetricCategory.Connector, "Connector calls that failed.", "key"),
        Metric.Duration(Latency, MetricCategory.Connector, "Connector call round-trip time.", "key"),
    ];
}

/// <summary>What the plugin framework is measured by.</summary>
public static class PluginMetricCollection
{
    /// <summary>Plugins that loaded.</summary>
    public const string Loaded = "plugin.loaded";

    /// <summary>Plugins that failed to load or start.</summary>
    public const string Failures = "plugin.failed";

    /// <summary>Plugins that were unloaded.</summary>
    public const string Unloaded = "plugin.unloaded";

    /// <summary>How long a plugin took to load.</summary>
    public const string LoadDuration = "plugin.load.duration";

    /// <summary>Gets the definitions in this collection.</summary>
    public static IReadOnlyList<MetricDefinition> Definitions { get; } =
    [
        Metric.Counter(Loaded, MetricCategory.Plugin, "Plugins loaded.", "key"),
        Metric.Counter(Failures, MetricCategory.Plugin, "Plugins that failed to load or start.", "key"),
        Metric.Counter(Unloaded, MetricCategory.Plugin, "Plugins unloaded.", "key"),
        Metric.Duration(LoadDuration, MetricCategory.Plugin, "Time taken to load a plugin.", "key"),
    ];
}

/// <summary>What inbound API traffic is measured by.</summary>
public static class ApiMetricCollection
{
    /// <summary>Requests the API handled.</summary>
    public const string Requests = "api.request";

    /// <summary>Requests that came back as an error.</summary>
    public const string Errors = "api.error";

    /// <summary>How long a request took to answer.</summary>
    public const string Latency = "api.latency";

    /// <summary>Gets the definitions in this collection.</summary>
    public static IReadOnlyList<MetricDefinition> Definitions { get; } =
    [
        Metric.Counter(Requests, MetricCategory.Api, "Requests handled.", "key", "outcome"),
        Metric.Counter(Errors, MetricCategory.Api, "Requests answered with an error.", "key"),
        Metric.Duration(Latency, MetricCategory.Api, "Request handling time.", "key"),
    ];
}

/// <summary>What the infrastructure the platform runs on is measured by.</summary>
public static class InfrastructureMetricCollection
{
    /// <summary>Queries issued against the database.</summary>
    public const string DatabaseQueries = "infrastructure.database.query";

    /// <summary>Database operations that failed.</summary>
    public const string DatabaseErrors = "infrastructure.database.error";

    /// <summary>How long a database operation took.</summary>
    public const string DatabaseLatency = "infrastructure.database.latency";

    /// <summary>Object storage operations.</summary>
    public const string StorageOperations = "infrastructure.storage.operation";

    /// <summary>Object storage operations that failed.</summary>
    public const string StorageErrors = "infrastructure.storage.error";

    /// <summary>How long an object storage operation took.</summary>
    public const string StorageLatency = "infrastructure.storage.latency";

    /// <summary>Cache reads that found what they were looking for.</summary>
    public const string CacheHits = "infrastructure.cache.hit";

    /// <summary>Cache reads that did not.</summary>
    public const string CacheMisses = "infrastructure.cache.miss";

    /// <summary>Messages published onto the event bus.</summary>
    public const string BrokerPublished = "infrastructure.broker.published";

    /// <summary>Publishes that failed.</summary>
    public const string BrokerFailures = "infrastructure.broker.failed";

    /// <summary>Configuration sections that were loaded or reloaded successfully.</summary>
    public const string ConfigurationApplied = "infrastructure.configuration.applied";

    /// <summary>Configuration sections that were rejected as invalid.</summary>
    public const string ConfigurationErrors = "infrastructure.configuration.error";

    /// <summary>Gets the definitions in this collection.</summary>
    public static IReadOnlyList<MetricDefinition> Definitions { get; } =
    [
        Metric.Counter(DatabaseQueries, MetricCategory.Infrastructure, "Database queries issued.", "key"),
        Metric.Counter(DatabaseErrors, MetricCategory.Infrastructure, "Database operations that failed.", "key"),
        Metric.Duration(DatabaseLatency, MetricCategory.Infrastructure, "Database operation time.", "key"),
        Metric.Counter(StorageOperations, MetricCategory.Infrastructure, "Object storage operations.", "key"),
        Metric.Counter(StorageErrors, MetricCategory.Infrastructure, "Object storage operations that failed.", "key"),
        Metric.Duration(StorageLatency, MetricCategory.Infrastructure, "Object storage operation time.", "key"),
        Metric.Counter(CacheHits, MetricCategory.Infrastructure, "Cache reads that hit.", "key"),
        Metric.Counter(CacheMisses, MetricCategory.Infrastructure, "Cache reads that missed.", "key"),
        Metric.Counter(BrokerPublished, MetricCategory.Infrastructure, "Messages published onto the bus.", "key"),
        Metric.Counter(BrokerFailures, MetricCategory.Infrastructure, "Publishes that failed.", "key"),
        Metric.Counter(
            ConfigurationApplied, MetricCategory.Infrastructure, "Configuration sections applied.", "key"),
        Metric.Counter(
            ConfigurationErrors, MetricCategory.Infrastructure, "Configuration sections rejected.", "key"),
    ];
}

/// <summary>What the process the platform runs in is measured by.</summary>
public static class RuntimeMetricCollection
{
    /// <summary>Managed memory in use.</summary>
    public const string MemoryBytes = "runtime.memory.bytes";

    /// <summary>Processor time the process is using.</summary>
    public const string CpuPercent = "runtime.cpu.percent";

    /// <summary>Threads the process is running.</summary>
    public const string Threads = "runtime.threads";

    /// <summary>Garbage collections that have run.</summary>
    public const string GarbageCollections = "runtime.gc.collections";

    /// <summary>How long the process has been up.</summary>
    public const string UptimeSeconds = "runtime.uptime.seconds";

    /// <summary>Gets the definitions in this collection.</summary>
    public static IReadOnlyList<MetricDefinition> Definitions { get; } =
    [
        Metric.Gauge(MemoryBytes, MetricCategory.Runtime, "bytes", "Managed memory in use."),
        Metric.Gauge(CpuPercent, MetricCategory.Runtime, "percent", "Processor time in use."),
        Metric.Gauge(Threads, MetricCategory.Runtime, "count", "Threads running."),
        Metric.Counter(GarbageCollections, MetricCategory.Runtime, "Garbage collections that have run.", "key"),
        Metric.Gauge(UptimeSeconds, MetricCategory.Runtime, "seconds", "How long the process has been up."),
    ];
}

/// <summary>
/// What cross-cutting performance is measured by — the measurements that belong to no single engine, such as
/// how deep a queue is or how long a named operation takes wherever it runs.
/// </summary>
public static class PerformanceMetricCollection
{
    /// <summary>How long a named operation took.</summary>
    public const string OperationDuration = "performance.operation.duration";

    /// <summary>How much work is waiting in a named queue.</summary>
    public const string QueueDepth = "performance.queue.depth";

    /// <summary>Units of work completed.</summary>
    public const string Throughput = "performance.throughput";

    /// <summary>Gets the definitions in this collection.</summary>
    public static IReadOnlyList<MetricDefinition> Definitions { get; } =
    [
        Metric.Duration(OperationDuration, MetricCategory.Performance, "Named operation run time.", "key"),
        Metric.Gauge(QueueDepth, MetricCategory.Performance, "count", "Work waiting in a named queue.", "key"),
        Metric.Counter(Throughput, MetricCategory.Performance, "Units of work completed.", "key"),
    ];
}
