using FactoryOS.Connectors.Runtime.Domain;
using FactoryOS.Connectors.Runtime.Events;
using FactoryOS.Connectors.Runtime.Persistence;
using FactoryOS.Domain.Abstractions;
using FactoryOS.Domain.Results;

namespace FactoryOS.Connectors.Runtime.Execution;

/// <summary>
/// The tenant-facing register of connector instances: creating them, finding them, reconfiguring them and
/// switching them on and off.
/// <para>
/// It is named for instances rather than connectors because the connector framework already has a
/// <c>ConnectorRegistry</c> holding the platform's live registrations. Two types called the same thing in one
/// assembly is a trap somebody eventually falls into, and the distinction is real: that registry knows which
/// connectors exist, this one knows which factory activated which of them, where and with what.
/// </para>
/// </summary>
public sealed class ConnectorInstanceRegistry
{
    private readonly IConnectorStore _instances;
    private readonly IConnectorRepository _definitions;
    private readonly IConnectorConfigurationRepository _configurations;
    private readonly ConnectorRuntimePublisher _events;
    private readonly IDateTimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="ConnectorInstanceRegistry"/> class.</summary>
    /// <param name="instances">The instance store.</param>
    /// <param name="definitions">The definition repository.</param>
    /// <param name="configurations">The configuration repository.</param>
    /// <param name="events">The event publisher.</param>
    /// <param name="clock">The clock.</param>
    public ConnectorInstanceRegistry(
        IConnectorStore instances,
        IConnectorRepository definitions,
        IConnectorConfigurationRepository configurations,
        ConnectorRuntimePublisher events,
        IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(instances);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(configurations);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(clock);

        _instances = instances;
        _definitions = definitions;
        _configurations = configurations;
        _events = events;
        _clock = clock;
    }

    /// <summary>Registers a tenant's activation of a connector definition.</summary>
    /// <param name="instance">The instance.</param>
    /// <returns>A successful result, or a failure explaining why it could not be registered.</returns>
    public Result Register(ConnectorInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (_definitions.Find(instance.DefinitionKey) is null)
        {
            return Result.Failure(Error.NotFound(
                "Connector.Instance.NoDefinition",
                $"Connector '{instance.DefinitionKey}' is not registered, so instance '{instance.Key}' has "
                + "nothing to activate."));
        }

        _instances.Save(instance);
        _configurations.Save(instance.Tenant, instance.Key, instance.Settings);
        return Result.Success();
    }

    /// <summary>Finds one tenant's instance.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="key">The instance key.</param>
    /// <returns>The instance, or <see langword="null"/> when the tenant has no such instance.</returns>
    public ConnectorInstance? Find(string tenant, string key) => _instances.Find(tenant, key);

    /// <summary>Lists one tenant's instances.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The instances.</returns>
    public IReadOnlyList<ConnectorInstance> ListByTenant(string tenant) => _instances.ListByTenant(tenant);

    /// <summary>Replaces an instance's endpoint, credential reference and settings, and announces the change.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="key">The instance key.</param>
    /// <param name="endpoint">The new endpoint.</param>
    /// <param name="changedBy">Who is changing it.</param>
    /// <param name="credential">The new credential reference.</param>
    /// <param name="settings">The new settings.</param>
    /// <returns>A successful result, or a failure when the tenant has no such instance.</returns>
    public Result Reconfigure(
        string tenant,
        string key,
        ConnectorEndpoint endpoint,
        string changedBy,
        ConnectorCredential? credential = null,
        IReadOnlyDictionary<string, string?>? settings = null)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(changedBy);

        var instance = _instances.Find(tenant, key);
        if (instance is null)
        {
            return Result.Failure(Error.NotFound(
                "Connector.Instance.NotFound", $"Tenant '{tenant}' has no connector instance '{key}'."));
        }

        instance.Reconfigure(endpoint, credential, settings);
        _instances.Save(instance);
        _configurations.Save(tenant, key, instance.Settings);

        _events.Publish(new ConnectorConfigurationChanged(key, changedBy)
        {
            Tenant = tenant,
            OccurredUtc = _clock.UtcNow,
        });

        return Result.Success();
    }

    /// <summary>Switches an instance on.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="key">The instance key.</param>
    /// <returns><see langword="true"/> when the tenant has such an instance.</returns>
    public bool Enable(string tenant, string key)
    {
        var instance = _instances.Find(tenant, key);
        if (instance is null)
        {
            return false;
        }

        instance.Enable();
        _instances.Save(instance);
        return true;
    }

    /// <summary>Switches an instance off; a disabled instance refuses invocations whatever its status.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="key">The instance key.</param>
    /// <returns><see langword="true"/> when the tenant has such an instance.</returns>
    public bool Disable(string tenant, string key)
    {
        var instance = _instances.Find(tenant, key);
        if (instance is null)
        {
            return false;
        }

        instance.Disable();
        _instances.Save(instance);
        return true;
    }

    /// <summary>Removes an instance and its stored configuration.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="key">The instance key.</param>
    /// <returns><see langword="true"/> when an instance was removed.</returns>
    public bool Remove(string tenant, string key)
    {
        _configurations.Remove(tenant, key);
        return _instances.Remove(tenant, key);
    }
}
