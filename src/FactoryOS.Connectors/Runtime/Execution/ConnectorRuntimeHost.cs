using FactoryOS.Connectors.Runtime.Domain;
using FactoryOS.Connectors.Runtime.Persistence;
using FactoryOS.Domain.Results;

namespace FactoryOS.Connectors.Runtime.Execution;

/// <summary>What one bulk start or stop did.</summary>
/// <param name="Started">The instance keys that started.</param>
/// <param name="Failed">The instance keys that did not, with the reason.</param>
public sealed record ConnectorHostSummary(
    IReadOnlyList<string> Started, IReadOnlyDictionary<string, string> Failed)
{
    /// <summary>Gets a value indicating whether every instance started.</summary>
    public bool AllStarted => Failed.Count == 0;
}

/// <summary>
/// Starts and stops a tenant's connector instances together.
/// <para>
/// One instance failing does not stop the rest. A factory whose Logo connector is misconfigured should still
/// get its scales, its PLCs and its label printers — refusing to start anything because one thing is wrong is
/// how a configuration error becomes an outage.
/// </para>
/// <para>
/// It is named for the runtime because the connector framework already hosts the platform's connect and
/// disconnect sequence; this one drives per-tenant instances, which the framework has no notion of.
/// </para>
/// </summary>
public sealed class ConnectorRuntimeHost
{
    private readonly ConnectorRuntime _runtime;
    private readonly IConnectorStore _instances;

    /// <summary>Initializes a new instance of the <see cref="ConnectorRuntimeHost"/> class.</summary>
    /// <param name="runtime">The connector runtime.</param>
    /// <param name="instances">The instance store.</param>
    public ConnectorRuntimeHost(ConnectorRuntime runtime, IConnectorStore instances)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(instances);
        _runtime = runtime;
        _instances = instances;
    }

    /// <summary>Starts every enabled instance a tenant has.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>What started and what did not, with reasons.</returns>
    public ConnectorHostSummary StartTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        var started = new List<string>();
        var failed = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var instance in _instances.ListByTenant(tenant).Where(instance => instance.Enabled))
        {
            var result = _runtime.Start(tenant, instance.Key);
            if (result.IsSuccess)
            {
                started.Add(instance.Key);
            }
            else
            {
                failed[instance.Key] = result.Error.Description;
            }
        }

        return new ConnectorHostSummary(started, failed);
    }

    /// <summary>Stops every one of a tenant's instances that is running.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="reason">Why they are being stopped.</param>
    /// <returns>How many instances were stopped.</returns>
    public int StopTenant(string tenant, string reason = "shutdown")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var stopped = 0;
        foreach (var instance in _instances.ListByTenant(tenant)
                     .Where(instance => instance.Status is ConnectorStatus.Running or ConnectorStatus.Degraded))
        {
            if (_runtime.Stop(tenant, instance.Key, reason) is { IsSuccess: true })
            {
                stopped++;
            }
        }

        return stopped;
    }

    /// <summary>Restarts one instance so a configuration change takes effect.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="key">The instance key.</param>
    /// <returns>A successful result, or the failure that stopped it.</returns>
    public Result Restart(string tenant, string key) => _runtime.Restart(tenant, key);
}
