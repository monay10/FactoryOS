using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugins.Workflow.Monitoring.Configuration;
using FactoryOS.Plugins.Workflow.Monitoring.Domain;
using FactoryOS.Plugins.Workflow.Monitoring.Events;
using FactoryOS.Plugins.Workflow.Monitoring.Persistence;

namespace FactoryOS.Plugins.Workflow.Monitoring.Execution;

/// <summary>
/// The health runtime: it runs the registered probes for a tenant, records what they found, and reports the
/// single status they add up to.
/// <para>
/// Two rules govern the aggregate. A component marked critical takes the whole platform down with it; a
/// non-critical one only degrades it — which is why marking everything critical would be the same as marking
/// nothing. And a component nothing has been heard from is <see cref="HealthStatus.Unknown"/> rather than
/// healthy: a report that calls silence health is a report that says "fine" during an outage.
/// </para>
/// </summary>
public sealed class HealthEngine
{
    private readonly HealthRegistry _registry;
    private readonly HealthCheckExecutor _executor;
    private readonly IHealthStore _results;
    private readonly IMetricStore _metrics;
    private readonly IMetricRepository _definitions;
    private readonly MetricAggregator _aggregator;
    private readonly MonitoringDispatcher _dispatcher;
    private readonly MonitoringEngineOptions _options;
    private readonly IDateTimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="HealthEngine"/> class.</summary>
    /// <param name="registry">The registry of checks and probes.</param>
    /// <param name="executor">The executor that contains probe failures.</param>
    /// <param name="results">The store of what probes found.</param>
    /// <param name="metrics">The series store probes read.</param>
    /// <param name="definitions">The definition registry.</param>
    /// <param name="aggregator">The aggregator.</param>
    /// <param name="dispatcher">The event dispatcher.</param>
    /// <param name="options">The engine options.</param>
    /// <param name="clock">The clock.</param>
    public HealthEngine(
        HealthRegistry registry,
        HealthCheckExecutor executor,
        IHealthStore results,
        IMetricStore metrics,
        IMetricRepository definitions,
        MetricAggregator aggregator,
        MonitoringDispatcher dispatcher,
        MonitoringEngineOptions options,
        IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(aggregator);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        _registry = registry;
        _executor = executor;
        _results = results;
        _metrics = metrics;
        _definitions = definitions;
        _aggregator = aggregator;
        _dispatcher = dispatcher;
        _options = options;
        _clock = clock;
    }

    /// <summary>Runs one check.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="checkKey">The check.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>What the probe found.</returns>
    /// <exception cref="InvalidOperationException">The check is not registered.</exception>
    public async Task<HealthCheckResult> CheckAsync(
        string tenant, string checkKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(checkKey);

        var registration = _registry.Find(checkKey)
            ?? throw new InvalidOperationException($"Health check '{checkKey}' is not registered.");

        return await RunAsync(tenant, registration.Check, registration.Probe, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Runs every registered check and reports what they add up to.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The report.</returns>
    public async Task<HealthReport> CheckAllAsync(string tenant, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        var registrations = _registry.All();
        var results = new List<HealthCheckResult>(registrations.Count);
        foreach (var (check, probe) in registrations)
        {
            results.Add(await RunAsync(tenant, check, probe, cancellationToken).ConfigureAwait(false));
        }

        return Report(tenant, results, registrations.Select(pair => pair.Check).ToArray());
    }

    /// <summary>Reports the last known state of every check without running any probe.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The report, built from the most recent result of each check.</returns>
    public HealthReport LastReport(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return Report(tenant, _results.LatestAll(tenant), _registry.Checks());
    }

    /// <summary>Gets the recorded history of a check.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="checkKey">The check.</param>
    /// <returns>The history, newest last.</returns>
    public IReadOnlyList<HealthCheckResult> History(string tenant, string checkKey) =>
        _results.History(tenant, checkKey);

    private async Task<HealthCheckResult> RunAsync(
        string tenant, HealthCheck check, HealthProbe probe, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var context = new HealthProbeContext(
            tenant, now, _options.HealthWindow, _metrics, _aggregator, _definitions);

        var result = await _executor.ExecuteAsync(check, probe, context, cancellationToken).ConfigureAwait(false);
        var previous = _results.Append(tenant, result);

        _dispatcher.Publish(new HealthCheckCompleted(tenant, now, result));

        // Only a transition is worth telling anyone about; "still healthy" is not news.
        if (previous is null || previous.Status != result.Status)
        {
            _dispatcher.Publish(new HealthStatusChanged(
                tenant, now, check.Key, previous?.Status ?? HealthStatus.Unknown, result.Status, result.Detail));
        }

        return result;
    }

    private HealthReport Report(
        string tenant, IReadOnlyList<HealthCheckResult> results, IReadOnlyList<HealthCheck> checks)
    {
        var critical = checks
            .Where(check => check.IsCritical)
            .Select(check => check.Key)
            .ToHashSet(StringComparer.Ordinal);

        var status = HealthStatus.Unknown;
        foreach (var result in results)
        {
            var contribution = result.Status switch
            {
                HealthStatus.Unhealthy when critical.Contains(result.Key) => HealthStatus.Unhealthy,
                HealthStatus.Unhealthy or HealthStatus.Degraded => HealthStatus.Degraded,
                HealthStatus.Healthy => HealthStatus.Healthy,
                _ => HealthStatus.Unknown,
            };

            // Unknown never overrides a status something actually reported; anything worse always does.
            if (contribution == HealthStatus.Unknown)
            {
                continue;
            }

            status = status == HealthStatus.Unknown ? contribution : Worse(status, contribution);
        }

        return new HealthReport(tenant, status, _clock.UtcNow, [.. results]);
    }

    private static HealthStatus Worse(HealthStatus left, HealthStatus right) => left > right ? left : right;
}
