namespace FactoryOS.Plugins.Workflow.Monitoring.Domain;

/// <summary>
/// A component the platform can be asked about, and what its answer means. The check is the description; the
/// probe that produces an answer is registered alongside it, so what a check <i>is</i> stays separate from how
/// it happens to be measured today.
/// </summary>
/// <param name="Key">The stable check key (for example <c>workflow-engine</c>).</param>
/// <param name="Name">The component's display name.</param>
/// <param name="Category">Which part of the platform the component belongs to.</param>
public sealed record HealthCheck(string Key, string Name, MetricCategory Category)
{
    /// <summary>Gets what the check looks at.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the platform is unhealthy when this component is. A critical component
    /// failing takes the whole report down; a non-critical one degrades it. Marking everything critical is the
    /// same as marking nothing critical, so this defaults to <see langword="false"/>.
    /// </summary>
    public bool IsCritical { get; init; }

    /// <summary>Gets how long the probe may run, or <see langword="null"/> to use the engine's default.</summary>
    public TimeSpan? Timeout { get; init; }
}

/// <summary>What a probe found.</summary>
/// <param name="Key">The check that was run.</param>
/// <param name="Status">What the probe concluded.</param>
/// <param name="Detail">Why — in a form a human on call can act on.</param>
/// <param name="CheckedOnUtc">When the probe ran.</param>
/// <param name="Duration">How long it took.</param>
/// <param name="Data">The numbers behind the conclusion, so the verdict can be argued with.</param>
public sealed record HealthCheckResult(
    string Key,
    HealthStatus Status,
    string Detail,
    DateTimeOffset CheckedOnUtc,
    TimeSpan Duration,
    IReadOnlyDictionary<string, string> Data)
{
    /// <summary>Creates a healthy result.</summary>
    /// <param name="key">The check.</param>
    /// <param name="checkedOnUtc">When it ran.</param>
    /// <param name="detail">Why it is healthy.</param>
    /// <param name="data">The supporting numbers.</param>
    /// <returns>The result.</returns>
    public static HealthCheckResult Healthy(
        string key,
        DateTimeOffset checkedOnUtc,
        string detail = "Healthy.",
        IReadOnlyDictionary<string, string>? data = null) =>
        new(key, HealthStatus.Healthy, detail, checkedOnUtc, TimeSpan.Zero, data ?? EmptyData);

    /// <summary>Creates a degraded result.</summary>
    /// <param name="key">The check.</param>
    /// <param name="checkedOnUtc">When it ran.</param>
    /// <param name="detail">What is worse than it should be.</param>
    /// <param name="data">The supporting numbers.</param>
    /// <returns>The result.</returns>
    public static HealthCheckResult Degraded(
        string key,
        DateTimeOffset checkedOnUtc,
        string detail,
        IReadOnlyDictionary<string, string>? data = null) =>
        new(key, HealthStatus.Degraded, detail, checkedOnUtc, TimeSpan.Zero, data ?? EmptyData);

    /// <summary>Creates an unhealthy result.</summary>
    /// <param name="key">The check.</param>
    /// <param name="checkedOnUtc">When it ran.</param>
    /// <param name="detail">What is failing.</param>
    /// <param name="data">The supporting numbers.</param>
    /// <returns>The result.</returns>
    public static HealthCheckResult Unhealthy(
        string key,
        DateTimeOffset checkedOnUtc,
        string detail,
        IReadOnlyDictionary<string, string>? data = null) =>
        new(key, HealthStatus.Unhealthy, detail, checkedOnUtc, TimeSpan.Zero, data ?? EmptyData);

    /// <summary>
    /// Creates a result for a component that produced no signal. Silence is not failure — a queue nobody used
    /// this hour is not broken — so it is reported as its own status rather than being rounded either way.
    /// </summary>
    /// <param name="key">The check.</param>
    /// <param name="checkedOnUtc">When it ran.</param>
    /// <param name="detail">Why nothing could be concluded.</param>
    /// <returns>The result.</returns>
    public static HealthCheckResult Unknown(string key, DateTimeOffset checkedOnUtc, string detail) =>
        new(key, HealthStatus.Unknown, detail, checkedOnUtc, TimeSpan.Zero, EmptyData);

    private static IReadOnlyDictionary<string, string> EmptyData { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// Every component's answer at one instant, plus the single status that follows from them. The overall status
/// is derived, never set: a report that could disagree with its own results would be worse than no report.
/// </summary>
/// <param name="Tenant">The tenant the report was produced for.</param>
/// <param name="Status">The status the results add up to.</param>
/// <param name="CheckedOnUtc">When the report was produced.</param>
/// <param name="Results">What each check found.</param>
public sealed record HealthReport(
    string Tenant, HealthStatus Status, DateTimeOffset CheckedOnUtc, IReadOnlyList<HealthCheckResult> Results)
{
    /// <summary>Gets the results that are not healthy, worst first.</summary>
    /// <returns>The results needing attention.</returns>
    public IReadOnlyList<HealthCheckResult> Failing() =>
        Results.Where(result => result.Status is HealthStatus.Degraded or HealthStatus.Unhealthy)
            .OrderByDescending(result => result.Status)
            .ToArray();
}
