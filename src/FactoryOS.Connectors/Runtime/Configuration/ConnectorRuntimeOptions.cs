namespace FactoryOS.Connectors.Runtime.Configuration;

/// <summary>
/// Stable constants for the connector <b>runtime</b> — the invocation layer above the connector framework.
/// The framework's own constants live in <see cref="Framework.Configuration.ConnectorConstants"/> and are
/// deliberately not restated here; these cover only what invoking an operation needs.
/// </summary>
public static class ConnectorRuntimeConstants
{
    /// <summary>The configuration section the runtime options bind from.</summary>
    public const string ConfigurationSection = "Connectors:Runtime";

    /// <summary>The prefix marking a configuration value as a reference to an externally held secret.</summary>
    public const string SecretPlaceholderPrefix = "${secret:";

    /// <summary>The suffix closing a secret reference.</summary>
    public const string SecretPlaceholderSuffix = "}";

    /// <summary>The masked form every resolved secret renders as, so no code path can print one by accident.</summary>
    public const string SecretMask = "***";

    /// <summary>The conventional operation name an inbound connector's record stream is invoked under.</summary>
    public const string ReadOperation = "read";

    /// <summary>The conventional operation name an outbound connector's delivery is invoked under.</summary>
    public const string DeliverOperation = "deliver";

    /// <summary>The label carrying the connector key on measurements the runtime reports.</summary>
    public const string ConnectorLabel = "connector";

    /// <summary>The label carrying the operation name on measurements the runtime reports.</summary>
    public const string OperationLabel = "operation";

    /// <summary>The label carrying the outcome on measurements the runtime reports.</summary>
    public const string OutcomeLabel = "outcome";
}

/// <summary>
/// The connector runtime's options, bound from <see cref="ConnectorRuntimeConstants.ConfigurationSection"/>.
/// Every value here is a <b>default</b>: a connector definition or a single operation may narrow it, and the
/// narrower value always wins. There is no setting that disables authorization, and none that lets a request
/// reach an instance belonging to another tenant — those are invariants, not policy.
/// </summary>
public sealed class ConnectorRuntimeOptions
{
    /// <summary>Gets or sets how long a single invocation attempt may take before it is abandoned.</summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets how many attempts a retryable failure is given, including the first.</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>Gets or sets the delay before the second attempt.</summary>
    public TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>Gets or sets the factor each successive retry delay is multiplied by.</summary>
    public double RetryBackoffMultiplier { get; set; } = 2.0;

    /// <summary>Gets or sets the ceiling no retry delay grows past.</summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets or sets how many consecutive failures open a circuit.</summary>
    public int CircuitFailureThreshold { get; set; } = 5;

    /// <summary>Gets or sets how long a circuit stays open before a single trial call is let through.</summary>
    public TimeSpan CircuitBreakDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets or sets how many invocations are permitted per <see cref="RateLimitWindow"/>.</summary>
    public int RateLimitPermits { get; set; } = 100;

    /// <summary>Gets or sets the window the rate limit's permits are counted over.</summary>
    public TimeSpan RateLimitWindow { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Gets or sets how long a cached response stays fresh.</summary>
    public TimeSpan CacheTimeToLive { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Gets or sets how many responses the cache holds before the oldest are evicted.</summary>
    public int CacheCapacity { get; set; } = 1000;

    /// <summary>Gets or sets how long a connector session may sit idle before it is closed.</summary>
    public TimeSpan SessionIdleTimeout { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Gets or sets a value indicating whether every invocation is announced on the event seam.</summary>
    public bool PublishInvocationEvents { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether successful invocations are audited, not only failures.</summary>
    public bool AuditSuccessfulInvocations { get; set; } = true;

    /// <summary>Gets or sets the directory whose immediate subfolders each hold one connector manifest.</summary>
    public string DiscoveryRoot { get; set; } = "connectors";
}
