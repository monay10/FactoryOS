namespace FactoryOS.Plugins.Workflow.Monitoring.Domain;

/// <summary>
/// How long raw measurements are kept, and what happens to them afterwards. A policy applies to one metric,
/// to a whole category, or — when it names neither — to everything; the most specific match wins, so a
/// platform-wide default can always be tightened for a noisy metric without being restated for the rest.
/// </summary>
/// <param name="RetainRaw">How long individual measurements are kept.</param>
/// <param name="Action">What happens to measurements older than that.</param>
public sealed record MetricRetentionPolicy(TimeSpan RetainRaw, MetricRetentionAction Action)
{
    /// <summary>Gets the metric this policy applies to, or <see langword="null"/> for any metric.</summary>
    public string? MetricKey { get; init; }

    /// <summary>Gets the category this policy applies to, or <see langword="null"/> for any category.</summary>
    public MetricCategory? Category { get; init; }

    /// <summary>
    /// Gets the bucket old measurements are collapsed into when <see cref="Action"/> is
    /// <see cref="MetricRetentionAction.RollUp"/>.
    /// </summary>
    public TimeSpan RollUpBucket { get; init; } = TimeSpan.FromHours(1);

    /// <summary>Gets the aggregation used to collapse each bucket.</summary>
    public MetricAggregation RollUpUsing { get; init; } = MetricAggregation.Average;

    /// <summary>
    /// Gets how specific this policy is; higher wins. Naming a metric is more specific than naming a category,
    /// which is more specific than naming nothing.
    /// </summary>
    public int Specificity => MetricKey is not null ? 2 : Category is not null ? 1 : 0;

    /// <summary>Gets a value indicating whether this policy applies to a definition.</summary>
    /// <param name="definition">The metric definition.</param>
    /// <returns><see langword="true"/> when the policy applies.</returns>
    public bool Matches(MetricDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (MetricKey is not null && !string.Equals(MetricKey, definition.Key, StringComparison.Ordinal))
        {
            return false;
        }

        return Category is null || Category == definition.Category;
    }
}

/// <summary>
/// A limit a metric is expected to stay within, and the states crossing it puts the metric in. A threshold
/// judges an aggregated <see cref="MetricSnapshot"/>, never a single measurement — one slow request is not an
/// incident, and a threshold that fired on one would train everybody to ignore it.
/// </summary>
/// <param name="Key">The stable threshold key.</param>
/// <param name="MetricKey">The metric the threshold watches.</param>
/// <param name="Comparison">Which side of the limit is acceptable.</param>
/// <param name="CriticalAt">The limit that puts the metric in <see cref="MetricHealthState.Critical"/>.</param>
public sealed record MetricThreshold(
    string Key, string MetricKey, MetricComparison Comparison, double CriticalAt)
{
    /// <summary>
    /// Gets the limit that puts the metric in <see cref="MetricHealthState.Warning"/>, or
    /// <see langword="null"/> when the threshold has no warning band.
    /// </summary>
    public double? WarningAt { get; init; }

    /// <summary>
    /// Gets the aggregation the threshold judges, or <see langword="null"/> to use the metric definition's
    /// default — the one that follows from its kind.
    /// </summary>
    public MetricAggregation? Aggregation { get; init; }

    /// <summary>Gets the window judged, or <see langword="null"/> to use the engine's default window.</summary>
    public TimeSpan? Window { get; init; }

    /// <summary>
    /// Gets the labels a series must carry for this threshold to judge it, or <see langword="null"/> to judge
    /// every series of the metric independently.
    /// </summary>
    public MetricDimension? Dimension { get; init; }

    /// <summary>Gets what the threshold is for.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Judges a snapshot against the limits.</summary>
    /// <param name="snapshot">The snapshot to judge.</param>
    /// <returns>
    /// The state the metric is in. An empty window is <see cref="MetricHealthState.Unknown"/> rather than
    /// <see cref="MetricHealthState.Ok"/>: no traffic is not evidence of health.
    /// </returns>
    public MetricHealthState Evaluate(MetricSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.IsEmpty)
        {
            return MetricHealthState.Unknown;
        }

        if (Crosses(snapshot.Value, CriticalAt))
        {
            return MetricHealthState.Critical;
        }

        return WarningAt is { } warning && Crosses(snapshot.Value, warning)
            ? MetricHealthState.Warning
            : MetricHealthState.Ok;
    }

    private bool Crosses(double value, double limit) => Comparison switch
    {
        MetricComparison.GreaterThan => value > limit,
        MetricComparison.GreaterThanOrEqual => value >= limit,
        MetricComparison.LessThan => value < limit,
        _ => value <= limit,
    };
}

/// <summary>
/// When a crossed threshold becomes something somebody is told about. The rule adds the two things a raw
/// threshold lacks: how bad it has to get, and for how long. <see cref="For"/> is what stops a single spike
/// from paging anyone — the breach has to survive that long before an alert opens.
/// </summary>
/// <param name="Key">The stable rule key.</param>
/// <param name="ThresholdKey">The threshold the rule watches.</param>
public sealed record MetricAlertRule(string Key, string ThresholdKey)
{
    /// <summary>Gets the state the metric must reach before the rule considers it breaching.</summary>
    public MetricHealthState TriggersAt { get; init; } = MetricHealthState.Critical;

    /// <summary>Gets how long the breach must persist before an alert opens; zero opens it immediately.</summary>
    public TimeSpan For { get; init; } = TimeSpan.Zero;

    /// <summary>Gets what the alert means and what to do about it.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether a state is severe enough to breach this rule.</summary>
    /// <param name="state">The state a threshold assigned.</param>
    /// <returns><see langword="true"/> when the state is at or beyond <see cref="TriggersAt"/>.</returns>
    public bool IsBreaching(MetricHealthState state) =>
        state != MetricHealthState.Unknown && state >= TriggersAt;
}

/// <summary>
/// An alert that is currently open, or the record of one that has closed.
/// <para>
/// Alerts are <b>derived</b>, not stored: they are what the retained series say right now. That is deliberate —
/// an alert that outlives the evidence for it is worse than no alert, because it keeps somebody looking at a
/// problem that has already gone. After a restart the evaluator re-derives what is still true, and consumers
/// deduplicate by alert key, exactly as the platform's at-least-once delivery already requires of them.
/// </para>
/// </summary>
/// <param name="Key">The alert's identity: the rule and the series it fired for.</param>
/// <param name="Tenant">The owning tenant.</param>
/// <param name="RuleKey">The rule that opened it.</param>
/// <param name="Instance">The series that breached.</param>
/// <param name="State">How severe the breach is.</param>
/// <param name="Value">The aggregated value that breached.</param>
/// <param name="FirstBreachedOnUtc">When the breach was first seen.</param>
/// <param name="TriggeredOnUtc">When the alert opened, or <see langword="null"/> while it is still pending.</param>
/// <param name="Correlation">The operation behind the most recent breaching measurement.</param>
public sealed record MetricAlert(
    string Key,
    string Tenant,
    string RuleKey,
    MetricInstance Instance,
    MetricHealthState State,
    double Value,
    DateTimeOffset FirstBreachedOnUtc,
    DateTimeOffset? TriggeredOnUtc,
    MetricCorrelation Correlation)
{
    /// <summary>Gets a value indicating whether the alert has opened, as opposed to still waiting out its delay.</summary>
    public bool IsOpen => TriggeredOnUtc is not null;

    /// <summary>Builds the identity of the alert a rule would raise for a series.</summary>
    /// <param name="ruleKey">The rule.</param>
    /// <param name="instance">The series.</param>
    /// <returns>The alert key.</returns>
    public static string KeyFor(string ruleKey, MetricInstance instance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleKey);
        ArgumentNullException.ThrowIfNull(instance);
        return $"{ruleKey}|{instance.Key}";
    }
}
