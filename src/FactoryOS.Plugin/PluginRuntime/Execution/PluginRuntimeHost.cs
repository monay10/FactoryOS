using FactoryOS.Plugins.Runtime.Discovery;
using FactoryOS.Plugins.Runtime.Domain;

namespace FactoryOS.Plugins.Runtime.Execution;

/// <summary>What happened when a tenant's plugins were brought up or taken down.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="Succeeded">How many plugins made the transition.</param>
/// <param name="Skipped">How many were skipped because they are switched off or not installed.</param>
/// <param name="Problems">What went wrong, one line per plugin, in the order it was attempted.</param>
public sealed record PluginHostSummary(
    string Tenant, int Succeeded, int Skipped, IReadOnlyList<string> Problems)
{
    /// <summary>Gets how many plugins failed.</summary>
    public int Failed => Problems.Count;

    /// <summary>Gets a value indicating whether every attempted plugin made the transition.</summary>
    public bool IsClean => Problems.Count == 0;
}

/// <summary>
/// Brings one tenant's plugins up and takes them down, in dependency order.
/// <para>
/// The name is deliberately not <c>PluginHost</c>: the framework already has one, which starts every plugin
/// in the process. This one starts <b>one factory's</b> plugins, from what that factory has installed, and
/// two factories can be brought up independently.
/// </para>
/// <para>
/// A plugin that fails never aborts the sweep. One broken plugin taking a whole factory's platform down with
/// it is a far worse failure than the one it started as; the others start, and the broken one is reported
/// with its reason.
/// </para>
/// </summary>
public sealed class PluginRuntimeHost
{
    private readonly IPluginLifecycleManager _lifecycle;
    private readonly PluginInstanceRegistry _registry;
    private readonly PluginRuntimeDependencyResolver _dependencies;

    /// <summary>Initializes a new instance of the <see cref="PluginRuntimeHost"/> class.</summary>
    /// <param name="lifecycle">The lifecycle manager.</param>
    /// <param name="registry">The instance registry.</param>
    /// <param name="dependencies">The dependency resolver deciding the order.</param>
    public PluginRuntimeHost(
        IPluginLifecycleManager lifecycle,
        PluginInstanceRegistry registry,
        PluginRuntimeDependencyResolver dependencies)
    {
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(dependencies);

        _lifecycle = lifecycle;
        _registry = registry;
        _dependencies = dependencies;
    }

    /// <summary>Loads and starts everything a tenant has installed and switched on.</summary>
    /// <param name="caller">Who is asking.</param>
    /// <param name="cancellationToken">A token to cancel the sweep.</param>
    /// <returns>What happened.</returns>
    public async Task<PluginHostSummary> StartTenantAsync(
        PluginCaller caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var ordered = Order(caller.Tenant, out var problems, out var skipped);
        var started = 0;

        foreach (var instance in ordered)
        {
            var loaded = await _lifecycle.LoadAsync(caller, instance.PluginKey, cancellationToken)
                .ConfigureAwait(false);
            if (loaded.IsFailure)
            {
                problems.Add($"{instance.PluginKey}: {loaded.Error.Description}");
                continue;
            }

            var start = await _lifecycle.StartAsync(caller, instance.PluginKey, cancellationToken)
                .ConfigureAwait(false);
            if (start.IsFailure)
            {
                problems.Add($"{instance.PluginKey}: {start.Error.Description}");
                continue;
            }

            started++;
        }

        return new PluginHostSummary(caller.Tenant, started, skipped, problems);
    }

    /// <summary>Stops everything a tenant is running, in reverse dependency order.</summary>
    /// <param name="caller">Who is asking.</param>
    /// <param name="cancellationToken">A token to cancel the sweep.</param>
    /// <returns>What happened.</returns>
    public async Task<PluginHostSummary> StopTenantAsync(
        PluginCaller caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var ordered = Order(caller.Tenant, out var problems, out var skipped).Reverse();
        var stopped = 0;

        foreach (var instance in ordered)
        {
            if (instance.Status is not (PluginRuntimeStatus.Running or PluginRuntimeStatus.Suspended))
            {
                skipped++;
                continue;
            }

            var stop = await _lifecycle.StopAsync(caller, instance.PluginKey, cancellationToken)
                .ConfigureAwait(false);
            if (stop.IsFailure)
            {
                problems.Add($"{instance.PluginKey}: {stop.Error.Description}");
                continue;
            }

            stopped++;
        }

        return new PluginHostSummary(caller.Tenant, stopped, skipped, problems);
    }

    private IReadOnlyList<PluginInstance> Order(string tenant, out List<string> problems, out int skipped)
    {
        problems = [];
        skipped = 0;

        var candidates = new List<PluginInstance>();
        var definitions = new List<PluginDefinition>();

        foreach (var instance in _registry.ForTenant(tenant))
        {
            if (!instance.Enabled || instance.Status == PluginRuntimeStatus.Removed)
            {
                skipped++;
                continue;
            }

            var definition = _registry.DefinitionFor(instance);
            if (definition is null)
            {
                problems.Add(
                    $"{instance.PluginKey}: the catalogue holds no definition at version {instance.Version}.");
                continue;
            }

            candidates.Add(instance);
            definitions.Add(definition);
        }

        var resolved = _dependencies.Resolve(definitions);
        if (resolved.IsFailure)
        {
            // Without an order there is no safe sweep at all: starting a dependent before its dependency is
            // exactly the failure the resolver exists to prevent.
            problems.Add(resolved.Error.Description);
            return [];
        }

        var byKey = candidates.ToDictionary(instance => instance.PluginKey, StringComparer.OrdinalIgnoreCase);
        return [.. resolved.Value.Select(definition => byKey[definition.Key])];
    }
}
