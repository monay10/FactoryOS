using System.Collections.Concurrent;

namespace FactoryOS.Plugins.Workflow.Tasks.Events;

/// <summary>The base of a human task lifecycle event raised by the runtime and published onto the event bus.</summary>
/// <param name="TaskId">The task the event concerns.</param>
/// <param name="Tenant">The owning tenant.</param>
/// <param name="OccurredOnUtc">When the event occurred.</param>
/// <param name="DefinitionKey">The task definition.</param>
public abstract record HumanTaskEvent(Guid TaskId, string Tenant, DateTimeOffset OccurredOnUtc, string DefinitionKey);

/// <summary>Raised when a task is created.</summary>
/// <param name="TaskId">The task.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="DefinitionKey">The definition.</param>
public sealed record HumanTaskCreated(Guid TaskId, string Tenant, DateTimeOffset OccurredOnUtc, string DefinitionKey)
    : HumanTaskEvent(TaskId, Tenant, OccurredOnUtc, DefinitionKey);

/// <summary>Raised when a task is assigned to a principal.</summary>
/// <param name="TaskId">The task.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="DefinitionKey">The definition.</param>
/// <param name="Assignee">The resolved assignee.</param>
public sealed record HumanTaskAssigned(
    Guid TaskId, string Tenant, DateTimeOffset OccurredOnUtc, string DefinitionKey, string? Assignee)
    : HumanTaskEvent(TaskId, Tenant, OccurredOnUtc, DefinitionKey);

/// <summary>Raised when a task is opened by its assignee.</summary>
/// <param name="TaskId">The task.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="DefinitionKey">The definition.</param>
public sealed record HumanTaskOpened(Guid TaskId, string Tenant, DateTimeOffset OccurredOnUtc, string DefinitionKey)
    : HumanTaskEvent(TaskId, Tenant, OccurredOnUtc, DefinitionKey);

/// <summary>Raised when a task is completed.</summary>
/// <param name="TaskId">The task.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="DefinitionKey">The definition.</param>
/// <param name="CompletedBy">Who completed it.</param>
public sealed record HumanTaskCompleted(
    Guid TaskId, string Tenant, DateTimeOffset OccurredOnUtc, string DefinitionKey, string? CompletedBy)
    : HumanTaskEvent(TaskId, Tenant, OccurredOnUtc, DefinitionKey);

/// <summary>Raised when a task is rejected.</summary>
/// <param name="TaskId">The task.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="DefinitionKey">The definition.</param>
/// <param name="RejectedBy">Who rejected it.</param>
public sealed record HumanTaskRejected(
    Guid TaskId, string Tenant, DateTimeOffset OccurredOnUtc, string DefinitionKey, string? RejectedBy)
    : HumanTaskEvent(TaskId, Tenant, OccurredOnUtc, DefinitionKey);

/// <summary>Raised when a task is cancelled.</summary>
/// <param name="TaskId">The task.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="DefinitionKey">The definition.</param>
public sealed record HumanTaskCancelled(Guid TaskId, string Tenant, DateTimeOffset OccurredOnUtc, string DefinitionKey)
    : HumanTaskEvent(TaskId, Tenant, OccurredOnUtc, DefinitionKey);

/// <summary>Raised when a task expires because its deadline passed with no completion or escalation.</summary>
/// <param name="TaskId">The task.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="DefinitionKey">The definition.</param>
public sealed record HumanTaskExpired(Guid TaskId, string Tenant, DateTimeOffset OccurredOnUtc, string DefinitionKey)
    : HumanTaskEvent(TaskId, Tenant, OccurredOnUtc, DefinitionKey);

/// <summary>Raised when a task is escalated to another principal.</summary>
/// <param name="TaskId">The task.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="DefinitionKey">The definition.</param>
/// <param name="Level">The new escalation level.</param>
/// <param name="Assignee">The new assignee.</param>
public sealed record HumanTaskEscalated(
    Guid TaskId, string Tenant, DateTimeOffset OccurredOnUtc, string DefinitionKey, int Level, string? Assignee)
    : HumanTaskEvent(TaskId, Tenant, OccurredOnUtc, DefinitionKey);

/// <summary>Raised when a task is reassigned to a new owner.</summary>
/// <param name="TaskId">The task.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="DefinitionKey">The definition.</param>
/// <param name="Assignee">The new assignee.</param>
public sealed record HumanTaskReassigned(
    Guid TaskId, string Tenant, DateTimeOffset OccurredOnUtc, string DefinitionKey, string? Assignee)
    : HumanTaskEvent(TaskId, Tenant, OccurredOnUtc, DefinitionKey);

/// <summary>Receives human task lifecycle events raised by the runtime. The seam onto the platform event bus.</summary>
public interface IHumanTaskEventSink
{
    /// <summary>Publishes a human task event.</summary>
    /// <param name="taskEvent">The event to publish.</param>
    void Publish(HumanTaskEvent taskEvent);
}

/// <summary>An in-memory <see cref="IHumanTaskEventSink"/> that records published events for inspection.</summary>
public sealed class InMemoryHumanTaskEventSink : IHumanTaskEventSink
{
    private readonly ConcurrentQueue<HumanTaskEvent> _events = new();

    /// <summary>Gets the published events in order.</summary>
    public IReadOnlyList<HumanTaskEvent> Events => _events.ToArray();

    /// <inheritdoc />
    public void Publish(HumanTaskEvent taskEvent)
    {
        ArgumentNullException.ThrowIfNull(taskEvent);
        _events.Enqueue(taskEvent);
    }
}
