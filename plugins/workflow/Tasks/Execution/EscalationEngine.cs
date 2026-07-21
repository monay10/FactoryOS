using FactoryOS.Plugins.Workflow.Tasks.Domain;

namespace FactoryOS.Plugins.Workflow.Tasks.Execution;

/// <summary>
/// Identifies a task's due escalations. An escalation whose due instant has passed and that has not yet been
/// applied is returned so the runtime can resolve its target assignment and hand the task over. Detection is
/// separated from application so the runtime keeps ownership of assignment resolution and event publication.
/// </summary>
public sealed class EscalationEngine
{
    /// <summary>Lists the escalations on a task that are due at or before an instant.</summary>
    /// <param name="instance">The task.</param>
    /// <param name="now">The current instant.</param>
    /// <returns>The due escalations, earliest first.</returns>
    public IReadOnlyList<HumanTaskEscalationState> DueEscalations(HumanTaskInstance instance, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (instance.IsFinished)
        {
            return [];
        }

        return instance.DueEscalations(now).OrderBy(escalation => escalation.DueAtUtc).ToArray();
    }

    /// <summary>Determines whether a task is overdue with no escalation to fall back on.</summary>
    /// <param name="instance">The task.</param>
    /// <param name="now">The current instant.</param>
    /// <returns>
    /// <see langword="true"/> when the deadline has passed and the task has neither been escalated nor has any
    /// escalation left to apply. A task that has escalated is handed to a new owner and does not auto-expire —
    /// it waits for that owner to act.
    /// </returns>
    public bool IsExpired(HumanTaskInstance instance, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return !instance.IsFinished
            && instance.EscalationLevel == 0
            && instance.DeadlineUtc is DateTimeOffset deadline
            && deadline <= now
            && instance.Escalations.All(escalation => escalation.Escalated);
    }
}
