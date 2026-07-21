using System.Collections.Concurrent;

namespace FactoryOS.Plugins.Workflow.Engine.Events;

/// <summary>The base of a workflow lifecycle event raised by the runtime.</summary>
/// <param name="InstanceId">The instance the event concerns.</param>
/// <param name="Tenant">The owning tenant.</param>
/// <param name="OccurredOnUtc">When the event occurred.</param>
public abstract record WorkflowEvent(Guid InstanceId, string Tenant, DateTimeOffset OccurredOnUtc);

/// <summary>Raised when an instance starts.</summary>
/// <param name="InstanceId">The instance.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="DefinitionKey">The definition that started.</param>
public sealed record WorkflowStarted(Guid InstanceId, string Tenant, DateTimeOffset OccurredOnUtc, string DefinitionKey)
    : WorkflowEvent(InstanceId, Tenant, OccurredOnUtc);

/// <summary>Raised when an instance completes successfully.</summary>
/// <param name="InstanceId">The instance.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
public sealed record WorkflowCompleted(Guid InstanceId, string Tenant, DateTimeOffset OccurredOnUtc)
    : WorkflowEvent(InstanceId, Tenant, OccurredOnUtc);

/// <summary>Raised when an instance is cancelled.</summary>
/// <param name="InstanceId">The instance.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
public sealed record WorkflowCancelled(Guid InstanceId, string Tenant, DateTimeOffset OccurredOnUtc)
    : WorkflowEvent(InstanceId, Tenant, OccurredOnUtc);

/// <summary>Raised when an instance faults.</summary>
/// <param name="InstanceId">The instance.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="Reason">The failure reason.</param>
public sealed record WorkflowFailed(Guid InstanceId, string Tenant, DateTimeOffset OccurredOnUtc, string Reason)
    : WorkflowEvent(InstanceId, Tenant, OccurredOnUtc);

/// <summary>Raised when an activity begins.</summary>
/// <param name="InstanceId">The instance.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="NodeId">The activity node.</param>
/// <param name="Assignee">The resolved assignee, if any.</param>
public sealed record ActivityStarted(
    Guid InstanceId, string Tenant, DateTimeOffset OccurredOnUtc, string NodeId, string? Assignee)
    : WorkflowEvent(InstanceId, Tenant, OccurredOnUtc);

/// <summary>Raised when an activity completes.</summary>
/// <param name="InstanceId">The instance.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="NodeId">The activity node.</param>
public sealed record ActivityCompleted(Guid InstanceId, string Tenant, DateTimeOffset OccurredOnUtc, string NodeId)
    : WorkflowEvent(InstanceId, Tenant, OccurredOnUtc);

/// <summary>Raised when an activity faults.</summary>
/// <param name="InstanceId">The instance.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="NodeId">The activity node.</param>
/// <param name="Reason">The failure reason.</param>
public sealed record ActivityFailed(
    Guid InstanceId, string Tenant, DateTimeOffset OccurredOnUtc, string NodeId, string Reason)
    : WorkflowEvent(InstanceId, Tenant, OccurredOnUtc);

/// <summary>Receives workflow lifecycle events raised by the runtime.</summary>
public interface IWorkflowEventSink
{
    /// <summary>Publishes a workflow event.</summary>
    /// <param name="workflowEvent">The event to publish.</param>
    void Publish(WorkflowEvent workflowEvent);
}

/// <summary>An in-memory <see cref="IWorkflowEventSink"/> that records published events for inspection.</summary>
public sealed class InMemoryWorkflowEventSink : IWorkflowEventSink
{
    private readonly ConcurrentQueue<WorkflowEvent> _events = new();

    /// <summary>Gets the published events in order.</summary>
    public IReadOnlyList<WorkflowEvent> Events => _events.ToArray();

    /// <inheritdoc />
    public void Publish(WorkflowEvent workflowEvent)
    {
        ArgumentNullException.ThrowIfNull(workflowEvent);
        _events.Enqueue(workflowEvent);
    }
}
