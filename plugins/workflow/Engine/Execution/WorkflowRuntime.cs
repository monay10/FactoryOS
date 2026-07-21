using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugins.Workflow.Engine.Domain;
using FactoryOS.Plugins.Workflow.Engine.Events;
using FactoryOS.Plugins.Workflow.Engine.Persistence;

namespace FactoryOS.Plugins.Workflow.Engine.Execution;

/// <summary>
/// Orchestrates one instance's execution around persistence and events: it builds the execution context,
/// runs an executor operation, persists the instance and raises the lifecycle events the executor does not
/// (started, cancelled). Resuming a waiting instance reloads it from the store and its definition from the
/// repository, so the runtime is the single place instance state is loaded and saved.
/// </summary>
public sealed class WorkflowRuntime
{
    private readonly IWorkflowStore _store;
    private readonly IWorkflowRepository _repository;
    private readonly WorkflowExecutor _executor;
    private readonly IDateTimeProvider _clock;
    private readonly IWorkflowServiceRegistry _services;
    private readonly IWorkflowEventSink _events;

    /// <summary>Initializes a new instance of the <see cref="WorkflowRuntime"/> class.</summary>
    /// <param name="store">The instance store.</param>
    /// <param name="repository">The definition repository.</param>
    /// <param name="executor">The execution core.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="services">The service registry.</param>
    /// <param name="events">The event sink.</param>
    public WorkflowRuntime(
        IWorkflowStore store,
        IWorkflowRepository repository,
        WorkflowExecutor executor,
        IDateTimeProvider clock,
        IWorkflowServiceRegistry services,
        IWorkflowEventSink events)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(events);

        _store = store;
        _repository = repository;
        _executor = executor;
        _clock = clock;
        _services = services;
        _events = events;
    }

    /// <summary>Starts a new instance of a definition and runs it to its first waiting point (or completion).</summary>
    /// <param name="definition">The definition to run.</param>
    /// <param name="instance">The instance to start.</param>
    /// <param name="cancellationToken">A token to cancel execution.</param>
    /// <returns>The execution result.</returns>
    public async Task<ExecutionResult> StartAsync(
        WorkflowDefinition definition, WorkflowInstance instance, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(instance);

        _store.Save(instance);
        _events.Publish(new WorkflowStarted(instance.Id, instance.Tenant, _clock.UtcNow, definition.Key));

        var context = new WorkflowExecutionContext(instance, definition, _clock, _services, _events);
        var result = await _executor.StartAsync(context, cancellationToken).ConfigureAwait(false);
        _store.Save(instance);
        return result;
    }

    /// <summary>Completes a pending activity on an instance.</summary>
    /// <param name="instanceId">The instance id.</param>
    /// <param name="nodeId">The activity node id.</param>
    /// <param name="outcome">Optional outcome variables to set.</param>
    /// <param name="cancellationToken">A token to cancel execution.</param>
    /// <returns>The execution result, or <see langword="null"/> when the instance is unknown.</returns>
    public Task<ExecutionResult?> CompleteActivityAsync(
        Guid instanceId,
        string nodeId,
        IReadOnlyDictionary<string, object?>? outcome,
        CancellationToken cancellationToken) =>
        ResumeAsync(instanceId, (context, ct) => _executor.ResumeActivityAsync(context, nodeId, outcome, ct), cancellationToken);

    /// <summary>Fires a due timer on an instance.</summary>
    /// <param name="instanceId">The instance id.</param>
    /// <param name="nodeId">The timer node id.</param>
    /// <param name="cancellationToken">A token to cancel execution.</param>
    /// <returns>The execution result, or <see langword="null"/> when the instance is unknown.</returns>
    public Task<ExecutionResult?> FireTimerAsync(Guid instanceId, string nodeId, CancellationToken cancellationToken) =>
        ResumeAsync(instanceId, (context, ct) => _executor.ResumeTimerAsync(context, nodeId, ct), cancellationToken);

    /// <summary>Delivers a signal to an instance.</summary>
    /// <param name="instanceId">The instance id.</param>
    /// <param name="signalName">The signal name.</param>
    /// <param name="cancellationToken">A token to cancel execution.</param>
    /// <returns>The execution result, or <see langword="null"/> when the instance is unknown.</returns>
    public Task<ExecutionResult?> SignalAsync(Guid instanceId, string signalName, CancellationToken cancellationToken) =>
        ResumeAsync(instanceId, (context, ct) => _executor.ResumeSignalAsync(context, signalName, ct), cancellationToken);

    /// <summary>Cancels a running instance.</summary>
    /// <param name="instanceId">The instance id.</param>
    /// <param name="cancellationToken">A token (unused; cancellation is synchronous).</param>
    /// <returns>The execution result, or <see langword="null"/> when the instance is unknown.</returns>
    public Task<ExecutionResult?> CancelAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var instance = _store.Get(instanceId);
        if (instance is null)
        {
            return Task.FromResult<ExecutionResult?>(null);
        }

        if (!instance.IsFinished)
        {
            instance.MarkCancelled();
            instance.History.Append(_clock.UtcNow, instance.DefinitionKey, WorkflowState.Completed, "cancelled");
            _events.Publish(new WorkflowCancelled(instance.Id, instance.Tenant, _clock.UtcNow));
            _store.Save(instance);
        }

        return Task.FromResult<ExecutionResult?>(ExecutionResult.From(instance));
    }

    private async Task<ExecutionResult?> ResumeAsync(
        Guid instanceId,
        Func<WorkflowExecutionContext, CancellationToken, Task<ExecutionResult>> operation,
        CancellationToken cancellationToken)
    {
        var instance = _store.Get(instanceId);
        if (instance is null)
        {
            return null;
        }

        if (instance.IsFinished)
        {
            return ExecutionResult.From(instance);
        }

        var definition = _repository.Get(instance.DefinitionKey, instance.Version)
            ?? throw new InvalidOperationException(
                $"Definition '{instance.DefinitionKey}' {instance.Version} is not registered.");

        var context = new WorkflowExecutionContext(instance, definition, _clock, _services, _events);
        var result = await operation(context, cancellationToken).ConfigureAwait(false);
        _store.Save(instance);
        return result;
    }
}
