using FactoryOS.Plugins.Workflow.Approvals.Domain;

namespace FactoryOS.Plugins.Workflow.Approvals.Execution;

/// <summary>
/// Resolves an approval definition's deadline policy onto an instance at start and schedules its reminders and
/// escalations relative to the resolved deadline. An approval with no deadline policy is left with none.
/// </summary>
public sealed class ApprovalDeadlineEngine
{
    /// <summary>Applies the deadline, reminders and escalations of a definition to a new instance.</summary>
    /// <param name="definition">The approval definition.</param>
    /// <param name="instance">The instance to schedule.</param>
    /// <param name="createdOnUtc">When the approval was created.</param>
    public void Schedule(ApprovalDefinition definition, ApprovalInstance instance, DateTimeOffset createdOnUtc)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(instance);

        if (definition.Deadline is null)
        {
            return;
        }

        var deadline = definition.Deadline.Resolve(createdOnUtc);
        instance.SetDeadline(deadline);

        foreach (var reminder in definition.Reminders)
        {
            instance.AddReminder(reminder.ResolveFireInstant(deadline));
        }

        foreach (var escalation in definition.Escalations)
        {
            instance.AddEscalation(escalation.ResolveDueInstant(deadline), escalation.To);
        }
    }
}

/// <summary>
/// Fires an approval's due reminders. A reminder that has come due and not yet fired is marked fired and
/// returned; the runtime records the audit entry and metric and notifies the pending approvers. Firing is
/// idempotent — a reminder fires at most once.
/// </summary>
public sealed class ApprovalReminderEngine
{
    /// <summary>Fires the reminders on an approval that are due at or before an instant.</summary>
    /// <param name="instance">The approval.</param>
    /// <param name="now">The current instant.</param>
    /// <returns>The reminders that fired on this pass.</returns>
    public IReadOnlyList<ApprovalReminderState> Fire(ApprovalInstance instance, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (instance.IsFinished)
        {
            return [];
        }

        var due = instance.DueReminders(now);
        foreach (var reminder in due)
        {
            instance.MarkReminderFired(reminder);
        }

        return due;
    }
}

/// <summary>
/// Identifies an approval's due escalations and detects expiry. An escalation whose due instant has passed and
/// that has not yet been applied is returned so the runtime can resolve its target and reassign the pending
/// steps. Detection is separated from application so the runtime keeps ownership of resolution and events.
/// </summary>
public sealed class ApprovalEscalationEngine
{
    /// <summary>Lists the escalations on an approval that are due at or before an instant.</summary>
    /// <param name="instance">The approval.</param>
    /// <param name="now">The current instant.</param>
    /// <returns>The due escalations, earliest first.</returns>
    public IReadOnlyList<ApprovalEscalationState> DueEscalations(ApprovalInstance instance, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (instance.IsFinished)
        {
            return [];
        }

        return instance.DueEscalations(now).OrderBy(escalation => escalation.DueAtUtc).ToArray();
    }

    /// <summary>Determines whether an approval is overdue with no escalation left to apply.</summary>
    /// <param name="instance">The approval.</param>
    /// <param name="now">The current instant.</param>
    /// <returns><see langword="true"/> when the deadline has passed and every escalation is spent.</returns>
    public bool IsExpired(ApprovalInstance instance, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return !instance.IsFinished
            && !instance.HasEscalated
            && instance.DeadlineUtc is DateTimeOffset deadline
            && deadline <= now
            && instance.Escalations.All(escalation => escalation.Escalated);
    }
}
