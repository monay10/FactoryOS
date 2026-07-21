using FactoryOS.Plugins.Workflow.Tasks.Domain;

namespace FactoryOS.Plugins.Workflow.Tasks.Execution;

/// <summary>
/// The pure state machine of a human task. Every method applies one transition, guards it, appends the audit
/// entry to the task's history, and returns that entry so the runtime can persist it and publish the matching
/// event. The executor never persists, publishes events or touches the workflow; the runtime does that around
/// it.
/// </summary>
public sealed class HumanTaskExecutor
{
    /// <summary>Records the creation of a task.</summary>
    /// <param name="instance">The task.</param>
    /// <param name="now">When it was created.</param>
    /// <param name="actor">Who created it.</param>
    /// <returns>The recorded history entry.</returns>
    public HumanTaskHistoryEntry Created(HumanTaskInstance instance, DateTimeOffset now, string? actor) =>
        Record(instance, now, "created", actor, null);

    /// <summary>Assigns a task and moves it into the waiting state.</summary>
    /// <param name="instance">The task.</param>
    /// <param name="assignee">The resolved assignee.</param>
    /// <param name="candidates">The candidate pool.</param>
    /// <param name="now">When it happened.</param>
    /// <returns>The recorded history entry.</returns>
    public HumanTaskHistoryEntry Assign(
        HumanTaskInstance instance, string? assignee, IReadOnlyList<string> candidates, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(candidates);
        EnsureActionable(instance);
        instance.SetCandidates(candidates);
        instance.AssignTo(assignee);
        return Record(instance, now, "assigned", assignee, null);
    }

    /// <summary>Opens a task for its assignee.</summary>
    /// <param name="instance">The task.</param>
    /// <param name="now">When it happened.</param>
    /// <param name="actor">Who opened it.</param>
    /// <returns>The recorded history entry.</returns>
    public HumanTaskHistoryEntry Open(HumanTaskInstance instance, DateTimeOffset now, string? actor)
    {
        ArgumentNullException.ThrowIfNull(instance);
        EnsureActionable(instance);
        instance.MarkInProgress();
        return Record(instance, now, "opened", actor, null);
    }

    /// <summary>Completes a task with a decision.</summary>
    /// <param name="instance">The task.</param>
    /// <param name="decision">The decision.</param>
    /// <param name="now">When it happened.</param>
    /// <returns>The recorded history entry.</returns>
    public HumanTaskHistoryEntry Complete(HumanTaskInstance instance, HumanTaskDecision decision, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(decision);
        EnsureActionable(instance);
        instance.Complete(decision);
        return Record(instance, now, "completed", decision.DecidedBy, decision.Outcome.ToString());
    }

    /// <summary>Rejects a task with a decision.</summary>
    /// <param name="instance">The task.</param>
    /// <param name="decision">The decision.</param>
    /// <param name="now">When it happened.</param>
    /// <returns>The recorded history entry.</returns>
    public HumanTaskHistoryEntry Reject(HumanTaskInstance instance, HumanTaskDecision decision, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(decision);
        EnsureActionable(instance);
        instance.Reject(decision);
        return Record(instance, now, "rejected", decision.DecidedBy, decision.Comment);
    }

    /// <summary>Cancels a task.</summary>
    /// <param name="instance">The task.</param>
    /// <param name="now">When it happened.</param>
    /// <param name="actor">Who cancelled it.</param>
    /// <param name="reason">An optional reason.</param>
    /// <returns>The recorded history entry.</returns>
    public HumanTaskHistoryEntry Cancel(
        HumanTaskInstance instance, DateTimeOffset now, string? actor, string? reason)
    {
        ArgumentNullException.ThrowIfNull(instance);
        EnsureActionable(instance);
        instance.Cancel();
        return Record(instance, now, "cancelled", actor, reason);
    }

    /// <summary>Expires a task whose deadline passed with no completion or remaining escalation.</summary>
    /// <param name="instance">The task.</param>
    /// <param name="now">When it happened.</param>
    /// <returns>The recorded history entry.</returns>
    public HumanTaskHistoryEntry Expire(HumanTaskInstance instance, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(instance);
        EnsureActionable(instance);
        instance.Expire();
        return Record(instance, now, "expired", null, null);
    }

    /// <summary>Escalates a task to a new assignee.</summary>
    /// <param name="instance">The task.</param>
    /// <param name="assignee">The new assignee.</param>
    /// <param name="now">When it happened.</param>
    /// <returns>The recorded history entry.</returns>
    public HumanTaskHistoryEntry Escalate(HumanTaskInstance instance, string? assignee, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(instance);
        EnsureActionable(instance);
        instance.Escalate(assignee);
        return Record(instance, now, "escalated", assignee, $"level {instance.EscalationLevel}");
    }

    /// <summary>Reassigns a task to a new owner.</summary>
    /// <param name="instance">The task.</param>
    /// <param name="assignee">The new assignee.</param>
    /// <param name="now">When it happened.</param>
    /// <param name="actor">Who reassigned it.</param>
    /// <returns>The recorded history entry.</returns>
    public HumanTaskHistoryEntry Reassign(
        HumanTaskInstance instance, string? assignee, DateTimeOffset now, string? actor)
    {
        ArgumentNullException.ThrowIfNull(instance);
        EnsureActionable(instance);
        instance.Reassign(assignee);
        return Record(instance, now, "reassigned", actor, assignee);
    }

    /// <summary>Adds a comment and records it.</summary>
    /// <param name="instance">The task.</param>
    /// <param name="comment">The comment.</param>
    /// <param name="now">When it happened.</param>
    /// <returns>The recorded history entry.</returns>
    public HumanTaskHistoryEntry Comment(HumanTaskInstance instance, HumanTaskComment comment, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(comment);
        instance.AddComment(comment);
        return Record(instance, now, "commented", comment.Author, comment.Visibility.ToString());
    }

    /// <summary>Adds an attachment reference and records it.</summary>
    /// <param name="instance">The task.</param>
    /// <param name="attachment">The attachment.</param>
    /// <param name="now">When it happened.</param>
    /// <returns>The recorded history entry.</returns>
    public HumanTaskHistoryEntry Attach(HumanTaskInstance instance, HumanTaskAttachment attachment, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(attachment);
        instance.AddAttachment(attachment);
        return Record(instance, now, "attached", attachment.AddedBy, attachment.FileName);
    }

    /// <summary>Records that a reminder fired.</summary>
    /// <param name="instance">The task.</param>
    /// <param name="now">When it fired.</param>
    /// <returns>The recorded history entry.</returns>
    public HumanTaskHistoryEntry Reminded(HumanTaskInstance instance, DateTimeOffset now) =>
        Record(instance, now, "reminded", null, null);

    private static HumanTaskHistoryEntry Record(
        HumanTaskInstance instance, DateTimeOffset now, string action, string? actor, string? detail)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var entry = new HumanTaskHistoryEntry(instance.Id, now, action, actor, detail);
        instance.History.Append(entry);
        return entry;
    }

    private static void EnsureActionable(HumanTaskInstance instance)
    {
        if (instance.IsFinished)
        {
            throw new InvalidOperationException(
                $"Human task '{instance.Id}' is '{instance.Status}' and can no longer change.");
        }
    }
}
