using FactoryOS.Connectors.Framework.Runtime;
using FactoryOS.Connectors.Runtime.Domain;
using FactoryOS.Connectors.Runtime.Health;
using FactoryOS.Connectors.Runtime.Persistence;
using FactoryOS.Domain.Results;

namespace FactoryOS.Connectors.Runtime.Execution;

/// <summary>
/// The public entry point to the connector runtime: load connectors, activate them per tenant, start and
/// stop them, invoke their operations, schedule recurring calls and read their health.
/// <para>
/// The connector runtime is <b>infrastructure</b>, and the dependency arrow points one way. A business
/// module never names Logo, SAP, an LDAP server or an MQTT broker — it names a tenant's connector instance
/// and an operation, and this engine reaches the outside world on its behalf. Deleting a connector folder
/// removes that door and changes nothing else.
/// </para>
/// </summary>
public sealed class ConnectorEngine
{
    private readonly ConnectorRuntime _runtime;
    private readonly ConnectorLoader _loader;
    private readonly ConnectorRuntimeHost _host;
    private readonly ConnectorScheduler _scheduler;
    private readonly ConnectorHealthEngine _health;
    private readonly IConnectorRepository _definitions;
    private readonly ConnectorMetrics _metrics;

    /// <summary>Initializes a new instance of the <see cref="ConnectorEngine"/> class.</summary>
    /// <param name="runtime">The connector runtime.</param>
    /// <param name="loader">The connector loader.</param>
    /// <param name="host">The per-tenant host.</param>
    /// <param name="scheduler">The scheduler.</param>
    /// <param name="health">The health engine.</param>
    /// <param name="definitions">The definition repository.</param>
    /// <param name="metrics">The runtime's own tally.</param>
    public ConnectorEngine(
        ConnectorRuntime runtime,
        ConnectorLoader loader,
        ConnectorRuntimeHost host,
        ConnectorScheduler scheduler,
        ConnectorHealthEngine health,
        IConnectorRepository definitions,
        ConnectorMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(health);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(metrics);

        _runtime = runtime;
        _loader = loader;
        _host = host;
        _scheduler = scheduler;
        _health = health;
        _definitions = definitions;
        _metrics = metrics;
    }

    /// <summary>Registers a definition and attaches the handler that performs its operations.</summary>
    /// <param name="definition">The definition.</param>
    /// <param name="handler">The handler.</param>
    /// <returns>A successful result, or a failure explaining why it could not be loaded.</returns>
    public Result Load(ConnectorDefinition definition, IConnectorOperationHandler handler) =>
        _loader.Load(definition, handler);

    /// <summary>Registers a definition without attaching anything that can perform it.</summary>
    /// <param name="definition">The definition.</param>
    /// <returns>A successful result, or the first incoherence found.</returns>
    public Result Register(ConnectorDefinition definition) => _loader.Register(definition);

    /// <summary>Scans a directory and registers every valid connector manifest it finds.</summary>
    /// <param name="rootDirectory">The directory whose child folders each hold one connector.</param>
    /// <returns>What the pass loaded and what it rejected, with reasons.</returns>
    public ConnectorLoadSummary Discover(string rootDirectory) => _loader.DiscoverAndRegister(rootDirectory);

    /// <summary>Removes a definition and detaches its handler.</summary>
    /// <param name="key">The definition key.</param>
    /// <returns><see langword="true"/> when a definition was removed.</returns>
    public bool Unload(string key) => _loader.Unload(key);

    /// <summary>Gets the loaded definitions.</summary>
    /// <returns>The definitions, ordered by key.</returns>
    public IReadOnlyList<ConnectorDefinition> Definitions() => _definitions.All();

    /// <summary>Gets the definitions that declare a capability — how a caller finds a connector without naming one.</summary>
    /// <param name="capability">The capability required.</param>
    /// <returns>The matching definitions.</returns>
    public IReadOnlyList<ConnectorDefinition> WithCapability(ConnectorCapability capability) =>
        _definitions.WithCapability(capability);

    /// <summary>Registers a tenant's activation of a connector.</summary>
    /// <param name="instance">The instance.</param>
    /// <returns>A successful result, or a failure explaining why it could not be registered.</returns>
    public Result Activate(ConnectorInstance instance) => _runtime.Instances.Register(instance);

    /// <summary>Gets the instance registry, for reconfiguring and switching instances on and off.</summary>
    public ConnectorInstanceRegistry Instances => _runtime.Instances;

    /// <summary>Gets the scheduler.</summary>
    public ConnectorScheduler Scheduler => _scheduler;

    /// <summary>Starts a tenant's instance.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="key">The instance key.</param>
    /// <returns>A successful result, or a failure explaining what stopped it.</returns>
    public Result Start(string tenant, string key) => _runtime.Start(tenant, key);

    /// <summary>Stops a tenant's instance.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="key">The instance key.</param>
    /// <param name="reason">Why it is being stopped.</param>
    /// <returns>A successful result, or a failure when the tenant has no such instance.</returns>
    public Result Stop(string tenant, string key, string reason = "requested") => _runtime.Stop(tenant, key, reason);

    /// <summary>Starts every enabled instance a tenant has.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>What started and what did not, with reasons.</returns>
    public ConnectorHostSummary StartTenant(string tenant) => _host.StartTenant(tenant);

    /// <summary>Stops every one of a tenant's running instances.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="reason">Why they are being stopped.</param>
    /// <returns>How many instances were stopped.</returns>
    public int StopTenant(string tenant, string reason = "shutdown") => _host.StopTenant(tenant, reason);

    /// <summary>Invokes an operation.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">A token to cancel the invocation.</param>
    /// <returns>The response; a failure is a value, not an exception.</returns>
    public Task<ConnectorResponse> InvokeAsync(
        ConnectorRequest request, CancellationToken cancellationToken = default) =>
        _runtime.InvokeAsync(request, cancellationToken);

    /// <summary>Runs every schedule that is due.</summary>
    /// <param name="cancellationToken">A token to cancel the pass.</param>
    /// <returns>Each schedule that ran, with the response it produced.</returns>
    public Task<IReadOnlyList<(ConnectorSchedule Schedule, ConnectorResponse Response)>> RunDueAsync(
        CancellationToken cancellationToken = default) => _scheduler.RunDueAsync(cancellationToken);

    /// <summary>Reports one instance's health.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="key">The instance key.</param>
    /// <returns>The report.</returns>
    public ConnectorHealthReport Health(string tenant, string key) => _health.Check(tenant, key);

    /// <summary>Reports every one of a tenant's instances.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The reports.</returns>
    public IReadOnlyList<ConnectorHealthReport> HealthOfTenant(string tenant) => _health.CheckTenant(tenant);

    /// <summary>Reads the runtime's counters as a snapshot.</summary>
    /// <returns>The snapshot.</returns>
    public ConnectorMetricsSnapshot Snapshot() => _metrics.Snapshot();
}
