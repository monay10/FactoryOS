using System.Collections.Concurrent;
using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugin.Health;
using FactoryOS.Plugins.Runtime.Domain;
using FactoryOS.Plugins.Runtime.Events;
using FactoryOS.Plugins.Runtime.Execution;
using FactoryOS.Plugins.Runtime.Isolation;
using FactoryOS.Plugins.Runtime.Security;

namespace FactoryOS.Plugins.Runtime.Health;

/// <summary>
/// Asks six separate questions about one plugin instance and reports the worst answer.
/// <para>
/// They are separate because they fail separately and are repaired differently. A plugin can be running and
/// missing a permission its manifest requires; it can hold every permission and depend on a plugin that is
/// stopped; it can be perfect and over its storage quota. Collapsing that into one boolean produces "not
/// working", which is the reading that starts a shift on a plugin nobody has diagnosed.
/// </para>
/// <para>
/// <b>Silence is not health.</b> An instance that has never been started reports <c>Unknown</c>, not
/// <c>Healthy</c> — a plugin nobody has run is not a plugin that works.
/// </para>
/// </summary>
public sealed class PluginHealthEngine
{
    private readonly ConcurrentDictionary<string, PluginHealthStatus> _last =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly PluginInstanceRegistry _registry;
    private readonly IPluginHealthService _heartbeats;
    private readonly PluginPermissionValidator _permissions;
    private readonly PluginSandbox _sandbox;
    private readonly PluginRuntimeAnnouncer _announcer;
    private readonly IDateTimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="PluginHealthEngine"/> class.</summary>
    /// <param name="registry">The instance registry.</param>
    /// <param name="heartbeats">The framework health service holding heartbeats.</param>
    /// <param name="permissions">The permission validator.</param>
    /// <param name="sandbox">The sandbox holding resource usage.</param>
    /// <param name="announcer">The event, audit and metric announcer.</param>
    /// <param name="clock">The clock.</param>
    public PluginHealthEngine(
        PluginInstanceRegistry registry,
        IPluginHealthService heartbeats,
        PluginPermissionValidator permissions,
        PluginSandbox sandbox,
        PluginRuntimeAnnouncer announcer,
        IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(heartbeats);
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(sandbox);
        ArgumentNullException.ThrowIfNull(announcer);
        ArgumentNullException.ThrowIfNull(clock);

        _registry = registry;
        _heartbeats = heartbeats;
        _permissions = permissions;
        _sandbox = sandbox;
        _announcer = announcer;
        _clock = clock;
    }

    /// <summary>Takes a health report for one tenant's plugin.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="pluginKey">The plugin key.</param>
    /// <returns>The report; an instance that is not installed reports every aspect as unknown.</returns>
    public PluginHealthReport Check(string tenant, string pluginKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginKey);

        var instance = _registry.Find(tenant, pluginKey);
        if (instance is null)
        {
            return new PluginHealthReport(tenant, pluginKey, _clock.UtcNow, [
                PluginHealthCheckResult.Unknown(
                    PluginHealthAspect.Liveness, $"Tenant '{tenant}' has not installed plugin '{pluginKey}'."),
            ]);
        }

        var definition = _registry.DefinitionFor(instance);

        var results = new List<PluginHealthCheckResult>
        {
            Liveness(instance),
            Readiness(instance),
            Dependencies(instance, definition),
            Version(instance, definition),
            Permissions(instance, definition),
            Resources(instance),
        };

        var report = new PluginHealthReport(tenant, pluginKey, _clock.UtcNow, results);
        Announce(instance, report);
        return report;
    }

    /// <summary>Takes a health report for everything one tenant has installed.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The reports.</returns>
    public IReadOnlyList<PluginHealthReport> CheckTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return [.. _registry.ForTenant(tenant).Select(instance => Check(instance.Tenant, instance.PluginKey))];
    }

    private void Announce(PluginInstance instance, PluginHealthReport report)
    {
        var previous = _last.TryGetValue(instance.Identity, out var last) ? last : PluginHealthStatus.Unknown;
        if (previous == report.Status && _last.ContainsKey(instance.Identity))
        {
            return;
        }

        _last[instance.Identity] = report.Status;

        var detail = report.Problems.Count == 0
            ? "Every aspect is healthy."
            : string.Join(" ", report.Problems.Select(problem => problem.Detail));

        _announcer.Publish(new PluginHealthChanged(
            instance.Tenant, instance.PluginKey, report.CheckedUtc, previous, report.Status, detail));
    }

    private static PluginHealthCheckResult Liveness(PluginInstance instance)
    {
        if (!instance.Enabled)
        {
            // An operator switching a plugin off is a decision, not a fault. Reporting it as unhealthy would
            // fill a dashboard with alarms about things that are exactly as intended.
            return PluginHealthCheckResult.Unknown(
                PluginHealthAspect.Liveness, "The plugin is switched off for this tenant.");
        }

        return instance.Status switch
        {
            PluginRuntimeStatus.Running => PluginHealthCheckResult.Healthy(
                PluginHealthAspect.Liveness, "The plugin is running."),
            PluginRuntimeStatus.Suspended => PluginHealthCheckResult.Degraded(
                PluginHealthAspect.Liveness, "The plugin is suspended and is refusing new work."),
            PluginRuntimeStatus.Failed => PluginHealthCheckResult.Unhealthy(
                PluginHealthAspect.Liveness, instance.FailureReason ?? "The plugin failed."),
            _ => PluginHealthCheckResult.Unknown(
                PluginHealthAspect.Liveness, $"The plugin is {instance.Status} and has not been started."),
        };
    }

    private PluginHealthCheckResult Readiness(PluginInstance instance)
    {
        if (instance.Status != PluginRuntimeStatus.Running)
        {
            return PluginHealthCheckResult.Unknown(
                PluginHealthAspect.Readiness, "The plugin is not running, so no heartbeat is expected.");
        }

        // The framework's heartbeat service is keyed by plugin key alone, so this answer is process-wide
        // rather than per tenant. Every other aspect here is per instance; this one is honestly narrower than
        // it looks, and reconciling it needs a commit permitted to change the framework.
        var health = _heartbeats.GetHealth(instance.PluginKey);

        return health.Status switch
        {
            PluginHealthStatus.Healthy => PluginHealthCheckResult.Healthy(
                PluginHealthAspect.Readiness, "The plugin is beating within its heartbeat window."),
            PluginHealthStatus.Degraded => PluginHealthCheckResult.Degraded(
                PluginHealthAspect.Readiness, health.Detail ?? "The plugin has recorded failures."),
            PluginHealthStatus.Unhealthy => PluginHealthCheckResult.Unhealthy(
                PluginHealthAspect.Readiness, health.Detail ?? "The plugin missed its heartbeat window."),
            _ => PluginHealthCheckResult.Unknown(
                PluginHealthAspect.Readiness, "No heartbeat has been recorded yet."),
        };
    }

    private PluginHealthCheckResult Dependencies(PluginInstance instance, PluginDefinition? definition)
    {
        if (definition is null)
        {
            return PluginHealthCheckResult.Unknown(
                PluginHealthAspect.Dependencies, "The catalogue holds no definition to read dependencies from.");
        }

        if (definition.Dependencies.Count == 0)
        {
            return PluginHealthCheckResult.Healthy(
                PluginHealthAspect.Dependencies, "The plugin depends on nothing.");
        }

        foreach (var dependency in definition.Dependencies)
        {
            var provider = _registry.Find(instance.Tenant, dependency.PluginKey);
            if (provider is null)
            {
                return PluginHealthCheckResult.Unhealthy(
                    PluginHealthAspect.Dependencies,
                    $"'{dependency.PluginKey}' is not installed for tenant '{instance.Tenant}'.");
            }

            if (!dependency.IsSatisfiedBy(provider.Version))
            {
                return PluginHealthCheckResult.Unhealthy(
                    PluginHealthAspect.Dependencies,
                    $"'{dependency.PluginKey}' is at {provider.Version}, below the required "
                    + $"{dependency.MinimumVersion}.");
            }

            if (!provider.CanServe)
            {
                return PluginHealthCheckResult.Degraded(
                    PluginHealthAspect.Dependencies,
                    $"'{dependency.PluginKey}' is installed but {provider.Status}.");
            }
        }

        return PluginHealthCheckResult.Healthy(
            PluginHealthAspect.Dependencies, "Every dependency is installed, satisfied and running.");
    }

    private static PluginHealthCheckResult Version(PluginInstance instance, PluginDefinition? definition)
    {
        if (definition is null)
        {
            return PluginHealthCheckResult.Unhealthy(
                PluginHealthAspect.Version,
                $"The catalogue no longer describes '{instance.PluginKey}' at {instance.Version}, so what is "
                + "running cannot be identified.");
        }

        return instance.CanRollback
            ? PluginHealthCheckResult.Healthy(
                PluginHealthAspect.Version,
                $"Running {instance.Version}; {instance.PreviousVersion} is retained for a rollback.")
            : PluginHealthCheckResult.Healthy(PluginHealthAspect.Version, $"Running {instance.Version}.");
    }

    private PluginHealthCheckResult Permissions(PluginInstance instance, PluginDefinition? definition)
    {
        if (definition is null)
        {
            return PluginHealthCheckResult.Unknown(
                PluginHealthAspect.Permissions, "The catalogue holds no definition to read requests from.");
        }

        var validated = _permissions.Validate(instance, definition);
        return validated.IsSuccess
            ? PluginHealthCheckResult.Healthy(
                PluginHealthAspect.Permissions, "The tenant grants everything the plugin asks for.")
            : PluginHealthCheckResult.Unhealthy(PluginHealthAspect.Permissions, validated.Error.Description);
    }

    private PluginHealthCheckResult Resources(PluginInstance instance)
    {
        var usages = _sandbox.Usages(instance);
        var limited = usages.Where(usage => usage.IsLimited).ToArray();

        if (limited.Length == 0)
        {
            return PluginHealthCheckResult.Healthy(
                PluginHealthAspect.Resources, "The plugin runs without a quota.");
        }

        var exceeded = limited.FirstOrDefault(usage => usage.IsExceeded);
        return exceeded is not null
            ? PluginHealthCheckResult.Unhealthy(
                PluginHealthAspect.Resources, $"The plugin is over its quota: {exceeded}.")
            : PluginHealthCheckResult.Healthy(
                PluginHealthAspect.Resources,
                "Within quota: " + string.Join(", ", limited.Select(usage => usage.ToString())) + ".");
    }
}
