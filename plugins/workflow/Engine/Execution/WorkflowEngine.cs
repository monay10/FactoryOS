using FactoryOS.Plugins.Workflow.Engine.Configuration;
using FactoryOS.Plugins.Workflow.Engine.Domain;
using FactoryOS.Plugins.Workflow.Engine.Persistence;

namespace FactoryOS.Plugins.Workflow.Engine.Execution;

/// <summary>
/// The public entry point to the workflow runtime (namespace <c>Engine.Execution</c>; distinct from the
/// reactive workflow module's <c>WorkflowEngine</c>). It resolves definitions from the repository, creates
/// instances and delegates their execution and resumption to the runtime.
/// </summary>
public sealed class WorkflowEngine
{
    private readonly IWorkflowRepository _repository;
    private readonly IWorkflowStore _store;
    private readonly WorkflowRuntime _runtime;
    private readonly WorkflowEngineOptions _options;

    /// <summary>Initializes a new instance of the <see cref="WorkflowEngine"/> class.</summary>
    /// <param name="repository">The definition repository.</param>
    /// <param name="store">The instance store.</param>
    /// <param name="runtime">The runtime.</param>
    /// <param name="options">The engine options.</param>
    public WorkflowEngine(
        IWorkflowRepository repository, IWorkflowStore store, WorkflowRuntime runtime, WorkflowEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(options);

        _repository = repository;
        _store = store;
        _runtime = runtime;
        _options = options;
    }

    /// <summary>Registers a definition so instances of it can be started and resumed.</summary>
    /// <param name="definition">The definition to register.</param>
    public void Register(WorkflowDefinition definition) => _repository.Register(definition);

    /// <summary>Starts the latest registered version of a definition.</summary>
    /// <param name="definitionKey">The definition key.</param>
    /// <param name="context">The workflow context (tenant, initiator).</param>
    /// <param name="variables">Optional seed variables.</param>
    /// <param name="cancellationToken">A token to cancel execution.</param>
    /// <returns>The execution result.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no definition is registered under the key.</exception>
    public Task<ExecutionResult> StartAsync(
        string definitionKey,
        WorkflowContext context,
        IReadOnlyDictionary<string, object?>? variables = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionKey);
        var definition = _repository.GetLatest(definitionKey)
            ?? throw new InvalidOperationException($"No workflow definition '{definitionKey}' is registered.");
        return StartInternalAsync(definition, context, variables, cancellationToken);
    }

    /// <summary>Starts an instance of a supplied definition, registering it first when configured to.</summary>
    /// <param name="definition">The definition to run.</param>
    /// <param name="context">The workflow context (tenant, initiator).</param>
    /// <param name="variables">Optional seed variables.</param>
    /// <param name="cancellationToken">A token to cancel execution.</param>
    /// <returns>The execution result.</returns>
    public Task<ExecutionResult> StartAsync(
        WorkflowDefinition definition,
        WorkflowContext context,
        IReadOnlyDictionary<string, object?>? variables = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (_options.AutoRegisterDefinitions)
        {
            _repository.Register(definition);
        }

        return StartInternalAsync(definition, context, variables, cancellationToken);
    }

    /// <summary>Completes a pending activity on an instance.</summary>
    /// <param name="instanceId">The instance id.</param>
    /// <param name="nodeId">The activity node id.</param>
    /// <param name="outcome">Optional outcome variables.</param>
    /// <param name="cancellationToken">A token to cancel execution.</param>
    /// <returns>The execution result, or <see langword="null"/> when the instance is unknown.</returns>
    public Task<ExecutionResult?> CompleteActivityAsync(
        Guid instanceId,
        string nodeId,
        IReadOnlyDictionary<string, object?>? outcome = null,
        CancellationToken cancellationToken = default) =>
        _runtime.CompleteActivityAsync(instanceId, nodeId, outcome, cancellationToken);

    /// <summary>Delivers a signal to an instance waiting on it.</summary>
    /// <param name="instanceId">The instance id.</param>
    /// <param name="signalName">The signal name.</param>
    /// <param name="cancellationToken">A token to cancel execution.</param>
    /// <returns>The execution result, or <see langword="null"/> when the instance is unknown.</returns>
    public Task<ExecutionResult?> SignalAsync(
        Guid instanceId, string signalName, CancellationToken cancellationToken = default) =>
        _runtime.SignalAsync(instanceId, signalName, cancellationToken);

    /// <summary>Cancels a running instance.</summary>
    /// <param name="instanceId">The instance id.</param>
    /// <param name="cancellationToken">A token to cancel execution.</param>
    /// <returns>The execution result, or <see langword="null"/> when the instance is unknown.</returns>
    public Task<ExecutionResult?> CancelAsync(Guid instanceId, CancellationToken cancellationToken = default) =>
        _runtime.CancelAsync(instanceId, cancellationToken);

    /// <summary>Gets an instance by id.</summary>
    /// <param name="instanceId">The instance id.</param>
    /// <returns>The instance, or <see langword="null"/> when not found.</returns>
    public WorkflowInstance? GetInstance(Guid instanceId) => _store.Get(instanceId);

    private Task<ExecutionResult> StartInternalAsync(
        WorkflowDefinition definition,
        WorkflowContext context,
        IReadOnlyDictionary<string, object?>? variables,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var instance = WorkflowInstance.Create(
            Guid.NewGuid(),
            definition.Key,
            definition.Version,
            context.Tenant,
            variables is null ? null : new WorkflowVariables(variables));
        return _runtime.StartAsync(definition, instance, cancellationToken);
    }
}
