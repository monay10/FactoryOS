using FactoryOS.Plugins.Runtime.Discovery;
using FactoryOS.Plugins.Runtime.Domain;

namespace FactoryOS.Plugins.Runtime.Execution;

/// <summary>What happened when a tenant was brought up from a cold start.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="Discovered">How many packages were found on disk.</param>
/// <param name="Installed">How many were installed.</param>
/// <param name="Started">How many started.</param>
/// <param name="Problems">Everything that went wrong, with its reason.</param>
public sealed record PluginBootstrapResult(
    string Tenant, int Discovered, int Installed, int Started, IReadOnlyList<string> Problems)
{
    /// <summary>Gets a value indicating whether every discovered package installed and started.</summary>
    public bool IsClean => Problems.Count == 0;
}

/// <summary>
/// Brings a factory up from a cold start: discover what is on disk, install it, then start it in dependency
/// order.
/// <para>
/// This is the composition, not another façade. <see cref="IPluginRuntime"/> exposes the individual
/// operations an engine or an administration screen performs one at a time; this type performs the sequence
/// a host runs once, at boot, when a tenant has nothing installed yet.
/// </para>
/// </summary>
public sealed class PluginEngine
{
    private readonly IPluginRuntime _runtime;
    private readonly PluginRuntimeHost _host;

    /// <summary>Initializes a new instance of the <see cref="PluginEngine"/> class.</summary>
    /// <param name="runtime">The runtime façade.</param>
    /// <param name="host">The per-tenant host.</param>
    public PluginEngine(IPluginRuntime runtime, PluginRuntimeHost host)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(host);

        _runtime = runtime;
        _host = host;
    }

    /// <summary>Discovers, installs and starts every package a tenant should run.</summary>
    /// <param name="caller">Who is asking.</param>
    /// <param name="granted">The permissions the tenant grants each installed plugin.</param>
    /// <param name="rootDirectory">The package root, or <see langword="null"/> to use the configured one.</param>
    /// <param name="cancellationToken">A token to cancel the sweep.</param>
    /// <returns>What happened.</returns>
    public async Task<PluginBootstrapResult> BootstrapAsync(
        PluginCaller caller,
        IEnumerable<PluginPermission> granted,
        string? rootDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(granted);

        var grants = granted as IReadOnlyList<PluginPermission> ?? [.. granted];
        var discovered = _runtime.Discover(rootDirectory);
        var problems = new List<string>();

        // A folder that could not be read is reported here rather than dropped: a plugin an operator put on
        // disk and never saw again is the hardest kind of missing.
        foreach (var rejection in discovered.Rejected)
        {
            problems.Add($"{rejection.Location}: {rejection.Reason}");
        }

        var installed = 0;
        foreach (var package in discovered.Packages)
        {
            var outcome = await _runtime.InstallAsync(caller, package, grants, cancellationToken)
                .ConfigureAwait(false);
            if (outcome.IsFailure)
            {
                problems.Add($"{package.Key}: {outcome.Error.Description}");
                continue;
            }

            installed++;
        }

        var summary = await _host.StartTenantAsync(caller, cancellationToken).ConfigureAwait(false);
        problems.AddRange(summary.Problems);

        return new PluginBootstrapResult(
            caller.Tenant, discovered.Count, installed, summary.Succeeded, problems);
    }
}
