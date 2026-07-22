using System.Collections.Concurrent;
using FactoryOS.Connectors.Framework.Health;
using FactoryOS.Connectors.Runtime.Discovery;
using FactoryOS.Connectors.Runtime.Domain;
using FactoryOS.Connectors.Runtime.Events;
using FactoryOS.Connectors.Runtime.Execution;
using FactoryOS.Connectors.Runtime.Persistence;
using FactoryOS.Connectors.Runtime.Security;
using FactoryOS.Domain.Abstractions;

namespace FactoryOS.Connectors.Runtime.Health;

/// <summary>
/// Answers five separate questions about a connector instance, and reports the worst answer as its health.
/// <para>
/// They are separate because they fail separately and are fixed differently. An instance can be running and
/// unable to authenticate; it can authenticate and be pointed at a version that no longer offers the
/// operation it uses; it can be perfect and its ERP down. A single boolean would collapse all of that into
/// "not working", which is the reading that starts a shift on a connector nobody has diagnosed.
/// </para>
/// </summary>
public sealed class ConnectorHealthEngine
{
    private readonly ConcurrentDictionary<string, ConnectorHealthStatus> _last = new(StringComparer.OrdinalIgnoreCase);
    private readonly IConnectorStore _instances;
    private readonly IConnectorRepository _definitions;
    private readonly ConnectorInvoker _invoker;
    private readonly ConnectorSecretResolver _secrets;
    private readonly CircuitBreakerEngine _breaker;
    private readonly IConnectorHealthService _dependency;
    private readonly VersionResolver _versions;
    private readonly ConnectorRuntimePublisher _events;
    private readonly IDateTimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="ConnectorHealthEngine"/> class.</summary>
    /// <param name="instances">The instance store.</param>
    /// <param name="definitions">The definition repository.</param>
    /// <param name="invoker">The invoker, consulted for an attached handler.</param>
    /// <param name="secrets">The secret resolver.</param>
    /// <param name="breaker">The circuit breaker engine.</param>
    /// <param name="dependency">The connector framework's health service.</param>
    /// <param name="versions">The version resolver.</param>
    /// <param name="events">The event publisher.</param>
    /// <param name="clock">The clock.</param>
    public ConnectorHealthEngine(
        IConnectorStore instances,
        IConnectorRepository definitions,
        ConnectorInvoker invoker,
        ConnectorSecretResolver secrets,
        CircuitBreakerEngine breaker,
        IConnectorHealthService dependency,
        VersionResolver versions,
        ConnectorRuntimePublisher events,
        IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(instances);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(invoker);
        ArgumentNullException.ThrowIfNull(secrets);
        ArgumentNullException.ThrowIfNull(breaker);
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentNullException.ThrowIfNull(versions);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(clock);

        _instances = instances;
        _definitions = definitions;
        _invoker = invoker;
        _secrets = secrets;
        _breaker = breaker;
        _dependency = dependency;
        _versions = versions;
        _events = events;
        _clock = clock;
    }

    /// <summary>Reports one instance's health, announcing the verdict when it has changed.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="key">The instance key.</param>
    /// <returns>The report; an unknown instance reports a single unhealthy liveness answer.</returns>
    public ConnectorHealthReport Check(string tenant, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var now = _clock.UtcNow;
        var instance = _instances.Find(tenant, key);
        if (instance is null)
        {
            return new ConnectorHealthReport(tenant, key, now,
            [
                ConnectorHealthCheckResult.Unhealthy(
                    ConnectorHealthAspect.Liveness, $"Tenant '{tenant}' has no connector instance '{key}'."),
            ]);
        }

        var definition = _definitions.Find(instance.DefinitionKey);
        var results = new List<ConnectorHealthCheckResult>
        {
            Liveness(instance),
            Readiness(instance, definition),
            Dependency(instance),
            Version(instance, definition),
            Credential(instance),
        };

        var report = new ConnectorHealthReport(tenant, key, now, results);
        Announce(instance, report);
        return report;
    }

    /// <summary>Reports every one of a tenant's instances.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The reports, ordered by instance key.</returns>
    public IReadOnlyList<ConnectorHealthReport> CheckTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return [.. _instances.ListByTenant(tenant).Select(instance => Check(tenant, instance.Key))];
    }

    private static ConnectorHealthCheckResult Liveness(ConnectorInstance instance) => instance.Status switch
    {
        ConnectorStatus.Running => ConnectorHealthCheckResult.Healthy(
            ConnectorHealthAspect.Liveness, "The instance is running."),
        ConnectorStatus.Degraded => ConnectorHealthCheckResult.Degraded(
            ConnectorHealthAspect.Liveness, instance.FailureReason ?? "The instance is running but impaired."),
        ConnectorStatus.Faulted => ConnectorHealthCheckResult.Unhealthy(
            ConnectorHealthAspect.Liveness, instance.FailureReason ?? "The instance faulted."),
        _ => ConnectorHealthCheckResult.Unhealthy(
            ConnectorHealthAspect.Liveness, $"The instance is {instance.Status}."),
    };

    private ConnectorHealthCheckResult Readiness(ConnectorInstance instance, ConnectorDefinition? definition)
    {
        if (!instance.Enabled)
        {
            return ConnectorHealthCheckResult.Unhealthy(
                ConnectorHealthAspect.Readiness, "The instance has been switched off.");
        }

        if (!instance.Endpoint.IsConfigured)
        {
            return ConnectorHealthCheckResult.Unhealthy(
                ConnectorHealthAspect.Readiness, "The instance has no endpoint configured.");
        }

        if (definition is null || _invoker.Find(definition.Key) is null)
        {
            return ConnectorHealthCheckResult.Unhealthy(
                ConnectorHealthAspect.Readiness,
                $"Nothing is attached that can perform '{instance.DefinitionKey}' operations.");
        }

        var open = definition.Operations
            .Select(operation => CircuitBreakerEngine.KeyFor(instance.Tenant, instance.Key, operation.Name))
            .Select(_breaker.Snapshot)
            .Where(snapshot => snapshot.State == CircuitState.Open)
            .ToArray();

        return open.Length == 0
            ? ConnectorHealthCheckResult.Healthy(
                ConnectorHealthAspect.Readiness, "The instance can accept an invocation.")
            : ConnectorHealthCheckResult.Degraded(
                ConnectorHealthAspect.Readiness,
                $"{open.Length} of {definition.Operations.Count} operations have an open circuit.");
    }

    private ConnectorHealthCheckResult Dependency(ConnectorInstance instance)
    {
        var health = _dependency.GetHealth(instance.DefinitionKey);
        return health.Status switch
        {
            ConnectorHealthStatus.Healthy => ConnectorHealthCheckResult.Healthy(
                ConnectorHealthAspect.Dependency, $"'{instance.DefinitionKey}' last answered successfully."),
            ConnectorHealthStatus.Degraded => ConnectorHealthCheckResult.Degraded(
                ConnectorHealthAspect.Dependency, health.Detail ?? "The external system has recorded failures."),
            ConnectorHealthStatus.Unhealthy => ConnectorHealthCheckResult.Unhealthy(
                ConnectorHealthAspect.Dependency, health.Detail ?? "The external system is not answering."),

            // Silence is not health. An instance nothing has called yet is unproven, not well, and saying so
            // is what stops a shift starting on a connector that has never once reached its ERP.
            _ => ConnectorHealthCheckResult.Unknown(
                ConnectorHealthAspect.Dependency, $"'{instance.DefinitionKey}' has not been reached yet."),
        };
    }

    private ConnectorHealthCheckResult Version(ConnectorInstance instance, ConnectorDefinition? definition)
    {
        if (definition is null)
        {
            return ConnectorHealthCheckResult.Unhealthy(
                ConnectorHealthAspect.Version, $"Connector '{instance.DefinitionKey}' is not registered.");
        }

        if (instance.MinimumVersion is not { } required)
        {
            return ConnectorHealthCheckResult.Healthy(
                ConnectorHealthAspect.Version, $"Running {definition.Version}; the instance pins no minimum.");
        }

        return _versions.Satisfies(definition.Version, required)
            ? ConnectorHealthCheckResult.Healthy(
                ConnectorHealthAspect.Version, $"Running {definition.Version}, which satisfies {required}.")
            : ConnectorHealthCheckResult.Unhealthy(
                ConnectorHealthAspect.Version,
                $"The instance requires {required} or a later version of the same major, but "
                + $"{definition.Version} is loaded.");
    }

    private ConnectorHealthCheckResult Credential(ConnectorInstance instance)
    {
        if (!instance.Credential.RequiresSecret)
        {
            return ConnectorHealthCheckResult.Healthy(
                ConnectorHealthAspect.Credential, "The external system needs no credential.");
        }

        var resolved = _secrets.ResolveFor(instance);
        return resolved.IsComplete
            ? ConnectorHealthCheckResult.Healthy(
                ConnectorHealthAspect.Credential, $"The {resolved.Kind} credential resolves.")
            : ConnectorHealthCheckResult.Unhealthy(
                ConnectorHealthAspect.Credential,
                $"The secret credential '{instance.Credential.Key}' refers to could not be resolved.");
    }

    private void Announce(ConnectorInstance instance, ConnectorHealthReport report)
    {
        var status = report.Status;
        var previous = _last.TryGetValue(instance.Identity, out var last) ? last : ConnectorHealthStatus.Unknown;
        _last[instance.Identity] = status;

        if (previous == status)
        {
            return;
        }

        var detail = report.Problems.Count == 0
            ? "Every aspect is healthy."
            : string.Join("; ", report.Problems.Select(problem => $"{problem.Aspect}: {problem.Detail}"));

        _events.Publish(new ConnectorHealthChanged(instance.Key, previous, status, detail)
        {
            Tenant = instance.Tenant,
            OccurredUtc = report.CheckedUtc,
        });
    }
}
