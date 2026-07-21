using FactoryOS.Plugins.Workflow.Notifications.Configuration;
using FactoryOS.Plugins.Workflow.Notifications.Domain;
using FactoryOS.Plugins.Workflow.Notifications.Execution;
using FactoryOS.Plugins.Workflow.Tasks.Events;

namespace FactoryOS.Plugins.Workflow.Notifications.Integration;

/// <summary>
/// Subscribes to the human task engine's events (by standing in as its <see cref="IHumanTaskEventSink"/>) and
/// turns them into notifications: a created task notifies the <see cref="NotificationCategory.HumanTask"/>
/// subscribers, an assigned or escalated task notifies its (new) assignee directly. It reads the task engine's
/// public events only; the task engine is never modified and never learns that notifications exist.
/// </summary>
public sealed class HumanTaskNotificationSubscriber : IHumanTaskEventSink
{
    private readonly NotificationEngine _engine;
    private readonly NotificationEngineOptions _options;

    /// <summary>Initializes a new instance of the <see cref="HumanTaskNotificationSubscriber"/> class.</summary>
    /// <param name="engine">The notification engine.</param>
    /// <param name="options">The engine options.</param>
    public HumanTaskNotificationSubscriber(NotificationEngine engine, NotificationEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(options);
        _engine = engine;
        _options = options;
    }

    /// <inheritdoc />
    public void Publish(HumanTaskEvent taskEvent)
    {
        ArgumentNullException.ThrowIfNull(taskEvent);
        if (!_options.SubscribeToEngineEvents)
        {
            return;
        }

        switch (taskEvent)
        {
            case HumanTaskCreated created:
                _engine.Notify(
                    new NotificationRequest
                    {
                        Category = NotificationCategory.HumanTask,
                        Source = "human-task",
                        SourceKey = created.DefinitionKey,
                        SourceId = created.TaskId,
                        Subject = "New task",
                        Body = $"Task '{created.DefinitionKey}' was created.",
                    },
                    new NotificationContext(created.Tenant));
                break;
            case HumanTaskAssigned { Assignee: { Length: > 0 } assignee } assigned:
                NotifyAssignee(assigned.Tenant, assigned.TaskId, assigned.DefinitionKey, assignee,
                    NotificationCategory.HumanTask, NotificationPriority.Normal, "Task assigned",
                    $"Task '{assigned.DefinitionKey}' was assigned to you.");
                break;
            case HumanTaskEscalated { Assignee: { Length: > 0 } assignee } escalated:
                NotifyAssignee(escalated.Tenant, escalated.TaskId, escalated.DefinitionKey, assignee,
                    NotificationCategory.Escalation, NotificationPriority.High, "Task escalated",
                    $"Task '{escalated.DefinitionKey}' was escalated to you.");
                break;
            default:
                break;
        }
    }

    private void NotifyAssignee(
        string tenant,
        Guid taskId,
        string definitionKey,
        string assignee,
        NotificationCategory category,
        NotificationPriority priority,
        string subject,
        string body) =>
        _engine.Notify(
            new NotificationRequest
            {
                Category = category,
                Source = "human-task",
                SourceKey = definitionKey,
                SourceId = taskId,
                Priority = priority,
                Recipients = [NotificationAssignment.ToUser(assignee)],
                Subject = subject,
                Body = body,
            },
            new NotificationContext(tenant));
}
