using FactoryOS.Plugins.Workflow.Approvals.Domain;

namespace FactoryOS.Plugins.Workflow.Approvals.Execution;

/// <summary>
/// The pure state machine of an approval. Every method applies one transition to the instance, appends the
/// paired audit entry to its history, and returns that entry so the runtime can persist it and publish the
/// matching event. The executor never persists, publishes events, resolves participants or touches the
/// workflow; the services do that around it.
/// </summary>
public sealed class ApprovalExecutor
{
    /// <summary>Records the creation of an approval.</summary>
    /// <param name="instance">The approval.</param>
    /// <param name="now">When it was created.</param>
    /// <param name="actor">Who created it.</param>
    /// <returns>The recorded history entry.</returns>
    public ApprovalHistoryEntry Created(ApprovalInstance instance, DateTimeOffset now, string? actor) =>
        Record(instance, now, "created", actor, null);

    /// <summary>Marks an approval as started.</summary>
    /// <param name="instance">The approval.</param>
    /// <param name="now">When it started.</param>
    /// <returns>The recorded history entry.</returns>
    public ApprovalHistoryEntry Start(ApprovalInstance instance, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(instance);
        instance.MarkStarted();
        return Record(instance, now, "started", null, null);
    }

    /// <summary>Activates a stage with its resolved steps.</summary>
    /// <param name="instance">The approval.</param>
    /// <param name="level">The stage level.</param>
    /// <param name="steps">The resolved steps.</param>
    /// <param name="now">When it happened.</param>
    /// <returns>The recorded history entry.</returns>
    public ApprovalHistoryEntry ActivateStage(
        ApprovalInstance instance, ApprovalLevel level, IReadOnlyList<ApprovalStep> steps, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(steps);
        instance.ActivateStage(level, steps);
        return Record(instance, now, "stage-activated", null, $"level {level.Value}");
    }

    /// <summary>Records a participant's vote.</summary>
    /// <param name="instance">The approval.</param>
    /// <param name="decision">The decision.</param>
    /// <param name="now">When it happened.</param>
    /// <returns>The recorded history entry.</returns>
    public ApprovalHistoryEntry Vote(ApprovalInstance instance, ApprovalDecision decision, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(decision);
        EnsureActionable(instance);
        instance.RecordDecision(decision);
        return Record(instance, now, $"voted:{decision.Kind}", decision.DecidedBy, decision.ParticipantId);
    }

    /// <summary>Finishes an approval with a terminal outcome.</summary>
    /// <param name="instance">The approval.</param>
    /// <param name="outcome">The terminal outcome.</param>
    /// <param name="now">When it happened.</param>
    /// <returns>The recorded history entry.</returns>
    public ApprovalHistoryEntry Finish(ApprovalInstance instance, ApprovalOutcome outcome, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(instance);
        EnsureActionable(instance);
        instance.Finish(outcome);
        return Record(instance, now, outcome == ApprovalOutcome.Approved ? "approved" : "rejected", null, null);
    }

    /// <summary>Cancels an approval.</summary>
    /// <param name="instance">The approval.</param>
    /// <param name="now">When it happened.</param>
    /// <param name="actor">Who cancelled it.</param>
    /// <param name="reason">An optional reason.</param>
    /// <returns>The recorded history entry.</returns>
    public ApprovalHistoryEntry Cancel(ApprovalInstance instance, DateTimeOffset now, string? actor, string? reason)
    {
        ArgumentNullException.ThrowIfNull(instance);
        EnsureActionable(instance);
        instance.Cancel();
        return Record(instance, now, "cancelled", actor, reason);
    }

    /// <summary>Expires an approval whose deadline passed with no decision.</summary>
    /// <param name="instance">The approval.</param>
    /// <param name="now">When it happened.</param>
    /// <returns>The recorded history entry.</returns>
    public ApprovalHistoryEntry Expire(ApprovalInstance instance, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(instance);
        EnsureActionable(instance);
        instance.Expire();
        return Record(instance, now, "expired", null, null);
    }

    /// <summary>Escalates an approval's pending steps to a new assignee.</summary>
    /// <param name="instance">The approval.</param>
    /// <param name="assignee">The new assignee.</param>
    /// <param name="now">When it happened.</param>
    /// <returns>The recorded history entry.</returns>
    public ApprovalHistoryEntry Escalate(ApprovalInstance instance, string assignee, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentException.ThrowIfNullOrWhiteSpace(assignee);
        EnsureActionable(instance);
        instance.EscalatePending(assignee);
        return Record(instance, now, "escalated", assignee, null);
    }

    /// <summary>Adds a comment and records it.</summary>
    /// <param name="instance">The approval.</param>
    /// <param name="comment">The comment.</param>
    /// <param name="now">When it happened.</param>
    /// <returns>The recorded history entry.</returns>
    public ApprovalHistoryEntry Comment(ApprovalInstance instance, ApprovalComment comment, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(comment);
        instance.AddComment(comment);
        return Record(instance, now, "commented", comment.Author, null);
    }

    /// <summary>Records that a reminder was sent.</summary>
    /// <param name="instance">The approval.</param>
    /// <param name="now">When it fired.</param>
    /// <returns>The recorded history entry.</returns>
    public ApprovalHistoryEntry ReminderSent(ApprovalInstance instance, DateTimeOffset now) =>
        Record(instance, now, "reminder-sent", null, null);

    private static ApprovalHistoryEntry Record(
        ApprovalInstance instance, DateTimeOffset now, string action, string? actor, string? detail)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var entry = new ApprovalHistoryEntry(instance.Id, now, action, actor, detail);
        instance.History.Append(entry);
        return entry;
    }

    private static void EnsureActionable(ApprovalInstance instance)
    {
        if (instance.IsFinished)
        {
            throw new InvalidOperationException(
                $"Approval '{instance.Id}' is '{instance.Status}' and can no longer change.");
        }
    }
}
