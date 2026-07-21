using System.Collections.Concurrent;

namespace FactoryOS.Plugins.Workflow.Approvals.Events;

/// <summary>The base of an approval lifecycle event raised by the runtime and published onto the event bus.</summary>
/// <param name="ApprovalId">The approval the event concerns.</param>
/// <param name="Tenant">The owning tenant.</param>
/// <param name="OccurredOnUtc">When the event occurred.</param>
/// <param name="DefinitionKey">The approval definition.</param>
public abstract record ApprovalEvent(Guid ApprovalId, string Tenant, DateTimeOffset OccurredOnUtc, string DefinitionKey);

/// <summary>Raised when an approval is created.</summary>
/// <param name="ApprovalId">The approval.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="DefinitionKey">The definition.</param>
public sealed record ApprovalCreated(Guid ApprovalId, string Tenant, DateTimeOffset OccurredOnUtc, string DefinitionKey)
    : ApprovalEvent(ApprovalId, Tenant, OccurredOnUtc, DefinitionKey);

/// <summary>Raised when an approval starts and its first stage becomes active.</summary>
/// <param name="ApprovalId">The approval.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="DefinitionKey">The definition.</param>
public sealed record ApprovalStarted(Guid ApprovalId, string Tenant, DateTimeOffset OccurredOnUtc, string DefinitionKey)
    : ApprovalEvent(ApprovalId, Tenant, OccurredOnUtc, DefinitionKey);

/// <summary>Raised when a stage's participant is assigned to an approver.</summary>
/// <param name="ApprovalId">The approval.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="DefinitionKey">The definition.</param>
/// <param name="ParticipantId">The participant.</param>
/// <param name="Assignee">The resolved assignee.</param>
public sealed record ApprovalAssigned(
    Guid ApprovalId, string Tenant, DateTimeOffset OccurredOnUtc, string DefinitionKey, string ParticipantId, string Assignee)
    : ApprovalEvent(ApprovalId, Tenant, OccurredOnUtc, DefinitionKey);

/// <summary>Raised when a participant approves.</summary>
/// <param name="ApprovalId">The approval.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="DefinitionKey">The definition.</param>
/// <param name="ParticipantId">The participant.</param>
/// <param name="DecidedBy">Who cast the vote.</param>
public sealed record ApprovalApproved(
    Guid ApprovalId, string Tenant, DateTimeOffset OccurredOnUtc, string DefinitionKey, string ParticipantId, string? DecidedBy)
    : ApprovalEvent(ApprovalId, Tenant, OccurredOnUtc, DefinitionKey);

/// <summary>Raised when a participant rejects.</summary>
/// <param name="ApprovalId">The approval.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="DefinitionKey">The definition.</param>
/// <param name="ParticipantId">The participant.</param>
/// <param name="DecidedBy">Who cast the vote.</param>
public sealed record ApprovalRejected(
    Guid ApprovalId, string Tenant, DateTimeOffset OccurredOnUtc, string DefinitionKey, string ParticipantId, string? DecidedBy)
    : ApprovalEvent(ApprovalId, Tenant, OccurredOnUtc, DefinitionKey);

/// <summary>Raised when an approval is cancelled.</summary>
/// <param name="ApprovalId">The approval.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="DefinitionKey">The definition.</param>
public sealed record ApprovalCancelled(Guid ApprovalId, string Tenant, DateTimeOffset OccurredOnUtc, string DefinitionKey)
    : ApprovalEvent(ApprovalId, Tenant, OccurredOnUtc, DefinitionKey);

/// <summary>Raised when an approval finishes with a final outcome.</summary>
/// <param name="ApprovalId">The approval.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="DefinitionKey">The definition.</param>
/// <param name="Approved">Whether the final outcome was an approval.</param>
public sealed record ApprovalCompleted(
    Guid ApprovalId, string Tenant, DateTimeOffset OccurredOnUtc, string DefinitionKey, bool Approved)
    : ApprovalEvent(ApprovalId, Tenant, OccurredOnUtc, DefinitionKey);

/// <summary>Raised when an approval expires because its deadline passed without a decision.</summary>
/// <param name="ApprovalId">The approval.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="DefinitionKey">The definition.</param>
public sealed record ApprovalExpired(Guid ApprovalId, string Tenant, DateTimeOffset OccurredOnUtc, string DefinitionKey)
    : ApprovalEvent(ApprovalId, Tenant, OccurredOnUtc, DefinitionKey);

/// <summary>Raised when an approval's pending steps are escalated to another approver.</summary>
/// <param name="ApprovalId">The approval.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="DefinitionKey">The definition.</param>
/// <param name="Assignee">The new assignee of the pending steps.</param>
public sealed record ApprovalEscalated(
    Guid ApprovalId, string Tenant, DateTimeOffset OccurredOnUtc, string DefinitionKey, string Assignee)
    : ApprovalEvent(ApprovalId, Tenant, OccurredOnUtc, DefinitionKey);

/// <summary>Raised when a reminder is sent for a pending approval.</summary>
/// <param name="ApprovalId">The approval.</param>
/// <param name="Tenant">The tenant.</param>
/// <param name="OccurredOnUtc">When it occurred.</param>
/// <param name="DefinitionKey">The definition.</param>
public sealed record ApprovalReminderSent(Guid ApprovalId, string Tenant, DateTimeOffset OccurredOnUtc, string DefinitionKey)
    : ApprovalEvent(ApprovalId, Tenant, OccurredOnUtc, DefinitionKey);

/// <summary>Receives approval lifecycle events raised by the runtime. The seam onto the platform event bus.</summary>
public interface IApprovalEventSink
{
    /// <summary>Publishes an approval event.</summary>
    /// <param name="approvalEvent">The event to publish.</param>
    void Publish(ApprovalEvent approvalEvent);
}

/// <summary>An in-memory <see cref="IApprovalEventSink"/> that records published events for inspection.</summary>
public sealed class InMemoryApprovalEventSink : IApprovalEventSink
{
    private readonly ConcurrentQueue<ApprovalEvent> _events = new();

    /// <summary>Gets the published events in order.</summary>
    public IReadOnlyList<ApprovalEvent> Events => _events.ToArray();

    /// <inheritdoc />
    public void Publish(ApprovalEvent approvalEvent)
    {
        ArgumentNullException.ThrowIfNull(approvalEvent);
        _events.Enqueue(approvalEvent);
    }
}
