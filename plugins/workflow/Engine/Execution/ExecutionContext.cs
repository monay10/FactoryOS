using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugins.Workflow.Engine.Domain;
using FactoryOS.Plugins.Workflow.Engine.Events;
using FactoryOS.Plugins.Workflow.Engine.Nodes;

namespace FactoryOS.Plugins.Workflow.Engine.Execution;

/// <summary>
/// The ambient data one execution run operates within: the instance and its definition, the clock, the
/// service registry and the event sink. It is the single object threaded through the executor so nodes and
/// services never reach into the host.
/// </summary>
public sealed class WorkflowExecutionContext
{
    /// <summary>Initializes a new instance of the <see cref="WorkflowExecutionContext"/> class.</summary>
    /// <param name="instance">The instance being executed.</param>
    /// <param name="definition">The definition being executed.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="services">The service registry.</param>
    /// <param name="events">The event sink.</param>
    public WorkflowExecutionContext(
        WorkflowInstance instance,
        WorkflowDefinition definition,
        IDateTimeProvider clock,
        IWorkflowServiceRegistry services,
        IWorkflowEventSink events)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(events);

        Instance = instance;
        Definition = definition;
        Clock = clock;
        Services = services;
        Events = events;
    }

    /// <summary>Gets the instance being executed.</summary>
    public WorkflowInstance Instance { get; }

    /// <summary>Gets the definition being executed.</summary>
    public WorkflowDefinition Definition { get; }

    /// <summary>Gets the clock.</summary>
    public IDateTimeProvider Clock { get; }

    /// <summary>Gets the service registry.</summary>
    public IWorkflowServiceRegistry Services { get; }

    /// <summary>Gets the event sink.</summary>
    public IWorkflowEventSink Events { get; }

    /// <summary>Gets the instance variables.</summary>
    public WorkflowVariables Variables => Instance.Variables;

    /// <summary>Creates a per-node execution scope.</summary>
    /// <param name="node">The node being executed.</param>
    /// <returns>The scope.</returns>
    public ExecutionScope ScopeFor(WorkflowNode node) => new(this, node);
}

/// <summary>
/// The scope of a single node's execution: the node together with the execution context. Services and node
/// handlers read and write the instance's variables through it.
/// </summary>
public sealed class ExecutionScope
{
    private readonly WorkflowExecutionContext _context;

    /// <summary>Initializes a new instance of the <see cref="ExecutionScope"/> class.</summary>
    /// <param name="context">The execution context.</param>
    /// <param name="node">The node being executed.</param>
    public ExecutionScope(WorkflowExecutionContext context, WorkflowNode node)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(node);
        _context = context;
        Node = node;
    }

    /// <summary>Gets the node being executed.</summary>
    public WorkflowNode Node { get; }

    /// <summary>Gets the instance being executed.</summary>
    public WorkflowInstance Instance => _context.Instance;

    /// <summary>Gets the instance variables.</summary>
    public WorkflowVariables Variables => _context.Instance.Variables;
}

/// <summary>The outcome of an execution run over an instance.</summary>
public sealed record ExecutionResult
{
    private ExecutionResult(Guid instanceId, WorkflowStatus status, string? failureReason)
    {
        InstanceId = instanceId;
        Status = status;
        FailureReason = failureReason;
    }

    /// <summary>Gets the instance id.</summary>
    public Guid InstanceId { get; }

    /// <summary>Gets the instance status after the run.</summary>
    public WorkflowStatus Status { get; }

    /// <summary>Gets the failure reason, when failed.</summary>
    public string? FailureReason { get; }

    /// <summary>Gets a value indicating whether the instance completed.</summary>
    public bool IsCompleted => Status == WorkflowStatus.Completed;

    /// <summary>Gets a value indicating whether the instance is still running (waiting on activities/timers/signals).</summary>
    public bool IsRunning => Status == WorkflowStatus.Running;

    /// <summary>Gets a value indicating whether the instance failed.</summary>
    public bool IsFailed => Status == WorkflowStatus.Failed;

    /// <summary>Captures the current state of an instance as a result.</summary>
    /// <param name="instance">The instance.</param>
    /// <returns>The result.</returns>
    public static ExecutionResult From(WorkflowInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return new ExecutionResult(instance.Id, instance.Status, instance.FailureReason);
    }
}
