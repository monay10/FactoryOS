using System.Collections.Concurrent;
using FactoryOS.Connectors.Framework.Runtime;
using FactoryOS.Connectors.Runtime.Domain;

namespace FactoryOS.Connectors.Runtime.Persistence;

/// <summary>
/// Holds the connector <b>definitions</b> the runtime knows about. Definitions are platform-wide: one
/// factory does not get a different Logo connector from another, only a different instance of it.
/// </summary>
public interface IConnectorRepository
{
    /// <summary>Adds or replaces a definition.</summary>
    /// <param name="definition">The definition.</param>
    void Register(ConnectorDefinition definition);

    /// <summary>Finds a definition by key.</summary>
    /// <param name="key">The definition key.</param>
    /// <returns>The definition, or <see langword="null"/> when none is registered under that key.</returns>
    ConnectorDefinition? Find(string key);

    /// <summary>Lists every registered definition, ordered by key.</summary>
    /// <returns>The definitions.</returns>
    IReadOnlyList<ConnectorDefinition> All();

    /// <summary>Lists the definitions that declare every requested capability.</summary>
    /// <param name="capability">The capability to filter by.</param>
    /// <returns>The matching definitions.</returns>
    IReadOnlyList<ConnectorDefinition> WithCapability(ConnectorCapability capability);

    /// <summary>Removes a definition.</summary>
    /// <param name="key">The definition key.</param>
    /// <returns><see langword="true"/> when a definition was removed.</returns>
    bool Remove(string key);
}

/// <summary>
/// Holds the tenant-scoped connector <b>instances</b>. Every method takes the tenant, and instances are
/// filed under a tenant-qualified identity, so there is no call shape that can return another tenant's
/// instance even by mistake.
/// </summary>
public interface IConnectorStore
{
    /// <summary>Adds or replaces an instance.</summary>
    /// <param name="instance">The instance.</param>
    void Save(ConnectorInstance instance);

    /// <summary>Finds one tenant's instance.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="key">The instance key.</param>
    /// <returns>The instance, or <see langword="null"/> when the tenant has no such instance.</returns>
    ConnectorInstance? Find(string tenant, string key);

    /// <summary>Lists one tenant's instances, ordered by key.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The instances.</returns>
    IReadOnlyList<ConnectorInstance> ListByTenant(string tenant);

    /// <summary>Lists every instance activating a definition, across tenants — for platform operations only.</summary>
    /// <param name="definitionKey">The definition key.</param>
    /// <returns>The instances.</returns>
    IReadOnlyList<ConnectorInstance> ListByDefinition(string definitionKey);

    /// <summary>Removes one tenant's instance.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="key">The instance key.</param>
    /// <returns><see langword="true"/> when an instance was removed.</returns>
    bool Remove(string tenant, string key);
}

/// <summary>
/// Holds the per-instance configuration the runtime applies at start-up, kept apart from the instances
/// themselves so configuration can be versioned, diffed and re-applied without rebuilding an instance.
/// </summary>
public interface IConnectorConfigurationRepository
{
    /// <summary>Stores an instance's configuration.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="instanceKey">The instance key.</param>
    /// <param name="values">The settings.</param>
    void Save(string tenant, string instanceKey, IReadOnlyDictionary<string, string?> values);

    /// <summary>Reads an instance's configuration.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="instanceKey">The instance key.</param>
    /// <returns>The settings, empty when none were stored.</returns>
    IReadOnlyDictionary<string, string?> Get(string tenant, string instanceKey);

    /// <summary>Removes an instance's configuration.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="instanceKey">The instance key.</param>
    /// <returns><see langword="true"/> when configuration was removed.</returns>
    bool Remove(string tenant, string instanceKey);
}

/// <summary>
/// Holds credential <b>references</b> per tenant. What is stored is where a secret lives, never the secret:
/// resolving the reference is the secret resolver's job and happens at invocation time.
/// </summary>
public interface IConnectorCredentialStore
{
    /// <summary>Stores a credential reference.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="credential">The credential reference.</param>
    void Save(string tenant, ConnectorCredential credential);

    /// <summary>Finds a credential reference.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="key">The credential key.</param>
    /// <returns>The credential, or <see langword="null"/> when the tenant has no such credential.</returns>
    ConnectorCredential? Find(string tenant, string key);

    /// <summary>Lists one tenant's credential references, ordered by key.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The credentials.</returns>
    IReadOnlyList<ConnectorCredential> ListByTenant(string tenant);

    /// <summary>Removes a credential reference.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="key">The credential key.</param>
    /// <returns><see langword="true"/> when a credential was removed.</returns>
    bool Remove(string tenant, string key);
}

/// <summary>The default in-memory <see cref="IConnectorRepository"/>.</summary>
public sealed class InMemoryConnectorRepository : IConnectorRepository
{
    private readonly ConcurrentDictionary<string, ConnectorDefinition> _definitions =
        new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void Register(ConnectorDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _definitions[definition.Key] = definition;
    }

    /// <inheritdoc />
    public ConnectorDefinition? Find(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _definitions.TryGetValue(key, out var definition) ? definition : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<ConnectorDefinition> All() =>
        [.. _definitions.Values.OrderBy(definition => definition.Key, StringComparer.Ordinal)];

    /// <inheritdoc />
    public IReadOnlyList<ConnectorDefinition> WithCapability(ConnectorCapability capability) =>
        [.. All().Where(definition => definition.Supports(capability))];

    /// <inheritdoc />
    public bool Remove(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _definitions.TryRemove(key, out _);
    }
}

/// <summary>The default in-memory <see cref="IConnectorStore"/>.</summary>
public sealed class InMemoryConnectorStore : IConnectorStore
{
    private readonly ConcurrentDictionary<string, ConnectorInstance> _instances =
        new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void Save(ConnectorInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        _instances[instance.Identity] = instance;
    }

    /// <inheritdoc />
    public ConnectorInstance? Find(string tenant, string key) =>
        _instances.TryGetValue(ConnectorInstance.Identify(tenant, key), out var instance) ? instance : null;

    /// <inheritdoc />
    public IReadOnlyList<ConnectorInstance> ListByTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return
        [
            .. _instances.Values
                .Where(instance => string.Equals(instance.Tenant, tenant, StringComparison.OrdinalIgnoreCase))
                .OrderBy(instance => instance.Key, StringComparer.Ordinal),
        ];
    }

    /// <inheritdoc />
    public IReadOnlyList<ConnectorInstance> ListByDefinition(string definitionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionKey);
        return
        [
            .. _instances.Values
                .Where(instance =>
                    string.Equals(instance.DefinitionKey, definitionKey, StringComparison.OrdinalIgnoreCase))
                .OrderBy(instance => instance.Identity, StringComparer.Ordinal),
        ];
    }

    /// <inheritdoc />
    public bool Remove(string tenant, string key) =>
        _instances.TryRemove(ConnectorInstance.Identify(tenant, key), out _);
}

/// <summary>The default in-memory <see cref="IConnectorConfigurationRepository"/>.</summary>
public sealed class InMemoryConnectorConfigurationRepository : IConnectorConfigurationRepository
{
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string?>> _configurations =
        new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void Save(string tenant, string instanceKey, IReadOnlyDictionary<string, string?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _configurations[ConnectorInstance.Identify(tenant, instanceKey)] =
            new Dictionary<string, string?>(values, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string?> Get(string tenant, string instanceKey) =>
        _configurations.TryGetValue(ConnectorInstance.Identify(tenant, instanceKey), out var values)
            ? values
            : new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public bool Remove(string tenant, string instanceKey) =>
        _configurations.TryRemove(ConnectorInstance.Identify(tenant, instanceKey), out _);
}

/// <summary>The default in-memory <see cref="IConnectorCredentialStore"/>.</summary>
public sealed class InMemoryConnectorCredentialStore : IConnectorCredentialStore
{
    private readonly ConcurrentDictionary<string, ConnectorCredential> _credentials =
        new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public void Save(string tenant, ConnectorCredential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);
        _credentials[ConnectorInstance.Identify(tenant, credential.Key)] = credential;
    }

    /// <inheritdoc />
    public ConnectorCredential? Find(string tenant, string key) =>
        _credentials.TryGetValue(ConnectorInstance.Identify(tenant, key), out var credential) ? credential : null;

    /// <inheritdoc />
    public IReadOnlyList<ConnectorCredential> ListByTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        var prefix = tenant + "|";
        return
        [
            .. _credentials
                .Where(pair => pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(pair => pair.Value)
                .OrderBy(credential => credential.Key, StringComparer.Ordinal),
        ];
    }

    /// <inheritdoc />
    public bool Remove(string tenant, string key) =>
        _credentials.TryRemove(ConnectorInstance.Identify(tenant, key), out _);
}
