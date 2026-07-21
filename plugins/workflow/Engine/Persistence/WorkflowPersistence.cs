using System.Collections.Concurrent;
using FactoryOS.Plugins.Workflow.Engine.Domain;

namespace FactoryOS.Plugins.Workflow.Engine.Persistence;

/// <summary>The registry of workflow definitions, keyed by definition key and version.</summary>
public interface IWorkflowRepository
{
    /// <summary>Registers a definition (idempotent by key and version).</summary>
    /// <param name="definition">The definition to register.</param>
    void Register(WorkflowDefinition definition);

    /// <summary>Gets a specific definition version.</summary>
    /// <param name="key">The definition key.</param>
    /// <param name="version">The version.</param>
    /// <returns>The definition, or <see langword="null"/> when not registered.</returns>
    WorkflowDefinition? Get(string key, WorkflowVersion version);

    /// <summary>Gets the highest registered version of a definition.</summary>
    /// <param name="key">The definition key.</param>
    /// <returns>The latest definition, or <see langword="null"/> when none is registered.</returns>
    WorkflowDefinition? GetLatest(string key);

    /// <summary>Gets every registered definition.</summary>
    /// <returns>The definitions.</returns>
    IReadOnlyCollection<WorkflowDefinition> All();
}

/// <summary>An in-memory <see cref="IWorkflowRepository"/>.</summary>
public sealed class InMemoryWorkflowRepository : IWorkflowRepository
{
    private readonly ConcurrentDictionary<string, WorkflowDefinition> _definitions = new(StringComparer.Ordinal);

    private static string KeyOf(string key, WorkflowVersion version) => $"{key}@{version.Value}";

    /// <inheritdoc />
    public void Register(WorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _definitions[KeyOf(definition.Key, definition.Version)] = definition;
    }

    /// <inheritdoc />
    public WorkflowDefinition? Get(string key, WorkflowVersion version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _definitions.TryGetValue(KeyOf(key, version), out var definition) ? definition : null;
    }

    /// <inheritdoc />
    public WorkflowDefinition? GetLatest(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _definitions.Values
            .Where(definition => string.Equals(definition.Key, key, StringComparison.Ordinal))
            .OrderByDescending(definition => definition.Version)
            .FirstOrDefault();
    }

    /// <inheritdoc />
    public IReadOnlyCollection<WorkflowDefinition> All() => _definitions.Values.ToArray();
}

/// <summary>The persistence store for workflow instances.</summary>
public interface IWorkflowStore
{
    /// <summary>Saves an instance (insert or update by id).</summary>
    /// <param name="instance">The instance to save.</param>
    void Save(WorkflowInstance instance);

    /// <summary>Gets an instance by id.</summary>
    /// <param name="id">The instance id.</param>
    /// <returns>The instance, or <see langword="null"/> when not found.</returns>
    WorkflowInstance? Get(Guid id);

    /// <summary>Lists the instances of a tenant.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The tenant's instances.</returns>
    IReadOnlyCollection<WorkflowInstance> ListByTenant(string tenant);

    /// <summary>Lists the instances in a given status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The matching instances.</returns>
    IReadOnlyCollection<WorkflowInstance> ListByStatus(WorkflowStatus status);
}

/// <summary>An in-memory <see cref="IWorkflowStore"/>. Instances are held by reference, so saves are updates.</summary>
public sealed class InMemoryWorkflowStore : IWorkflowStore
{
    private readonly ConcurrentDictionary<Guid, WorkflowInstance> _instances = new();

    /// <inheritdoc />
    public void Save(WorkflowInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        _instances[instance.Id] = instance;
    }

    /// <inheritdoc />
    public WorkflowInstance? Get(Guid id) => _instances.TryGetValue(id, out var instance) ? instance : null;

    /// <inheritdoc />
    public IReadOnlyCollection<WorkflowInstance> ListByTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return _instances.Values
            .Where(instance => string.Equals(instance.Tenant, tenant, StringComparison.Ordinal))
            .ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyCollection<WorkflowInstance> ListByStatus(WorkflowStatus status) =>
        _instances.Values.Where(instance => instance.Status == status).ToArray();
}
