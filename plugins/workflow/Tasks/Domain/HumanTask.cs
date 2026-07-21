namespace FactoryOS.Plugins.Workflow.Tasks.Domain;

/// <summary>
/// A lightweight summary of a human task for lists and inboxes: its identity, title, status, assignee,
/// priority and deadline — projected from a <see cref="HumanTaskInstance"/> without exposing its mutable
/// internals.
/// </summary>
/// <param name="Id">The task id.</param>
/// <param name="DefinitionKey">The definition the task runs.</param>
/// <param name="Title">The display title.</param>
/// <param name="Status">The current status.</param>
/// <param name="Priority">The current priority.</param>
/// <param name="Category">The category.</param>
/// <param name="Assignee">The current assignee, if any.</param>
/// <param name="DeadlineUtc">The deadline, if any.</param>
/// <param name="Tenant">The owning tenant.</param>
public sealed record HumanTask(
    Guid Id,
    string DefinitionKey,
    string Title,
    HumanTaskStatus Status,
    HumanTaskPriority Priority,
    HumanTaskCategory Category,
    string? Assignee,
    DateTimeOffset? DeadlineUtc,
    string Tenant)
{
    /// <summary>Projects a summary from a task instance.</summary>
    /// <param name="instance">The instance to summarize.</param>
    /// <returns>The summary.</returns>
    public static HumanTask From(HumanTaskInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return new HumanTask(
            instance.Id,
            instance.DefinitionKey,
            instance.Title,
            instance.Status,
            instance.Priority,
            instance.Category,
            instance.Assignee,
            instance.DeadlineUtc,
            instance.Tenant);
    }
}
