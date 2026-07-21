using FactoryOS.Plugins.Workflow.Tasks.Domain;

namespace FactoryOS.Plugins.Workflow.Tasks.Execution;

/// <summary>
/// Resolves a task definition's deadline policy onto an instance at creation and schedules its reminders and
/// escalations relative to the resolved deadline. A task with no deadline policy is left with none.
/// </summary>
public sealed class DeadlineEngine
{
    /// <summary>Applies the deadline, reminders and escalations of a definition to a new instance.</summary>
    /// <param name="definition">The task definition.</param>
    /// <param name="instance">The instance to schedule.</param>
    /// <param name="createdOnUtc">When the task was created.</param>
    public void Schedule(HumanTaskDefinition definition, HumanTaskInstance instance, DateTimeOffset createdOnUtc)
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
