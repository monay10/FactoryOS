using System.Collections.Concurrent;
using FactoryOS.Plugins.Workflow.Monitoring.Domain;
using FactoryOS.Plugins.Workflow.Monitoring.Persistence;

namespace FactoryOS.Plugins.Workflow.Monitoring.Execution;

/// <summary>What one alert evaluation changed.</summary>
/// <param name="Triggered">The alerts that opened.</param>
/// <param name="Resolved">The alerts that closed, with how long each was open.</param>
public sealed record AlertChanges(
    IReadOnlyList<MetricAlert> Triggered, IReadOnlyList<(MetricAlert Alert, TimeSpan OpenFor)> Resolved)
{
    /// <summary>An evaluation that changed nothing.</summary>
    public static AlertChanges None { get; } = new([], []);

    /// <summary>Gets a value indicating whether anything opened or closed.</summary>
    public bool IsEmpty => Triggered.Count == 0 && Resolved.Count == 0;
}

/// <summary>
/// Turns threshold verdicts into alerts that open and close.
/// <para>
/// Two decisions here are what separate an alert somebody acts on from one everybody mutes. First, a rule's
/// <c>For</c> delay must elapse with the breach unbroken before the alert opens, so a single spike never pages
/// anybody. Second — and less obvious — a metric that goes <b>silent</b> does not resolve an open alert. A
/// series that stops producing has not recovered; more often it has stopped because whatever produced it died,
/// and an alerting layer that treats silence as recovery closes the alert at exactly the moment it matters most.
/// </para>
/// </summary>
public sealed class AlertEvaluator
{
    private readonly IMetricRepository _rules;
    private readonly ConcurrentDictionary<string, MetricAlert> _alerts = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="AlertEvaluator"/> class.</summary>
    /// <param name="rules">The alert rule registry.</param>
    public AlertEvaluator(IMetricRepository rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules;
    }

    /// <summary>Applies threshold verdicts to the alert rules watching them.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="evaluations">The verdicts reached this pass.</param>
    /// <param name="nowUtc">The instant the verdicts describe.</param>
    /// <returns>The alerts that opened and closed.</returns>
    public AlertChanges Evaluate(
        string tenant, IReadOnlyList<ThresholdEvaluation> evaluations, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentNullException.ThrowIfNull(evaluations);

        var rules = _rules.AlertRules();
        if (rules.Count == 0 || evaluations.Count == 0)
        {
            return AlertChanges.None;
        }

        var triggered = new List<MetricAlert>();
        var resolved = new List<(MetricAlert, TimeSpan)>();

        foreach (var evaluation in evaluations)
        {
            foreach (var rule in rules.Where(rule =>
                string.Equals(rule.ThresholdKey, evaluation.Threshold.Key, StringComparison.Ordinal)))
            {
                Apply(tenant, rule, evaluation, nowUtc, triggered, resolved);
            }
        }

        return triggered.Count == 0 && resolved.Count == 0
            ? AlertChanges.None
            : new AlertChanges(triggered, resolved);
    }

    /// <summary>Gets the alerts currently open for a tenant.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The open alerts.</returns>
    public IReadOnlyList<MetricAlert> OpenAlerts(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return _alerts.Values
            .Where(alert => alert.IsOpen && string.Equals(alert.Tenant, tenant, StringComparison.Ordinal))
            .OrderBy(alert => alert.Key, StringComparer.Ordinal)
            .ToArray();
    }

    private void Apply(
        string tenant,
        MetricAlertRule rule,
        ThresholdEvaluation evaluation,
        DateTimeOffset nowUtc,
        List<MetricAlert> triggered,
        List<(MetricAlert, TimeSpan)> resolved)
    {
        var key = MetricAlert.KeyFor(rule.Key, evaluation.Snapshot.Instance);
        var existing = _alerts.TryGetValue(key, out var current) ? current : null;

        if (evaluation.State == MetricHealthState.Unknown)
        {
            // Silence is not recovery. Whatever was open stays open until a real measurement says otherwise.
            return;
        }

        if (!rule.IsBreaching(evaluation.State))
        {
            if (existing is null || !_alerts.TryRemove(key, out _))
            {
                return;
            }

            if (existing.TriggeredOnUtc is { } openedAt)
            {
                resolved.Add((existing, nowUtc - openedAt));
            }

            return;
        }

        var firstBreach = existing?.FirstBreachedOnUtc ?? nowUtc;
        var opened = existing?.TriggeredOnUtc;
        var opensNow = opened is null && nowUtc - firstBreach >= rule.For;

        var alert = new MetricAlert(
            key,
            tenant,
            rule.Key,
            evaluation.Snapshot.Instance,
            evaluation.State,
            evaluation.Snapshot.Value,
            firstBreach,
            opensNow ? nowUtc : opened,
            evaluation.Snapshot.Correlation);

        _alerts[key] = alert;
        if (opensNow)
        {
            triggered.Add(alert);
        }
    }
}
