using FactoryOS.Plugins.Workflow.Tasks.Domain;

namespace FactoryOS.Plugins.Workflow.Tasks.Execution;

/// <summary>
/// Fires a task's due reminders. A reminder that has come due and not yet fired is marked fired and returned;
/// the caller (the runtime) records the audit entry and metric and notifies the assignee through the event
/// bus. Firing is idempotent — a reminder fires at most once.
/// </summary>
public sealed class ReminderEngine
{
    /// <summary>Fires the reminders on a task that are due at or before an instant.</summary>
    /// <param name="instance">The task.</param>
    /// <param name="now">The current instant.</param>
    /// <returns>The reminders that fired on this pass.</returns>
    public IReadOnlyList<HumanTaskReminderState> Fire(HumanTaskInstance instance, DateTimeOffset now)
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
