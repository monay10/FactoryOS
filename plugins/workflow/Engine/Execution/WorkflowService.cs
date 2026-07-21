namespace FactoryOS.Plugins.Workflow.Engine.Execution;

/// <summary>
/// An in-process service a <see cref="Nodes.ServiceNode"/> invokes by key. Services read and write the
/// instance variables through the supplied scope; they are the seam for wiring automated steps into a
/// process without the runtime knowing what they do.
/// </summary>
public interface IWorkflowService
{
    /// <summary>Gets the key that a service node references to invoke this service.</summary>
    string Key { get; }

    /// <summary>Executes the service for a node.</summary>
    /// <param name="scope">The execution scope (instance, node and variables).</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task that completes when the service has run.</returns>
    Task ExecuteAsync(ExecutionScope scope, CancellationToken cancellationToken);
}

/// <summary>Resolves workflow services by key.</summary>
public interface IWorkflowServiceRegistry
{
    /// <summary>Finds a service by key.</summary>
    /// <param name="key">The service key.</param>
    /// <returns>The service, or <see langword="null"/> when none is registered under that key.</returns>
    IWorkflowService? Find(string key);
}

/// <summary>Default <see cref="IWorkflowServiceRegistry"/> indexing the registered services by key.</summary>
public sealed class WorkflowServiceRegistry : IWorkflowServiceRegistry
{
    private readonly Dictionary<string, IWorkflowService> _services;

    /// <summary>Initializes a new instance of the <see cref="WorkflowServiceRegistry"/> class.</summary>
    /// <param name="services">The registered services.</param>
    public WorkflowServiceRegistry(IEnumerable<IWorkflowService> services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services.ToDictionary(service => service.Key, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public IWorkflowService? Find(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _services.TryGetValue(key, out var service) ? service : null;
    }
}
