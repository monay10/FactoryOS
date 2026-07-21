using FactoryOS.Plugins.Workflow.Engine.Events;
using FactoryOS.Plugins.Workflow.Notifications.Configuration;
using FactoryOS.Plugins.Workflow.Notifications.Domain;
using FactoryOS.Plugins.Workflow.Notifications.Execution;

namespace FactoryOS.Plugins.Workflow.Notifications.Integration;

/// <summary>
/// Subscribes to the workflow runtime's events (by standing in as its <see cref="IWorkflowEventSink"/>) and
/// turns the notable ones — a workflow finishing or failing — into notifications for the subscribers of the
/// <see cref="NotificationCategory.Workflow"/> category. It reads the workflow's public events only; the
/// workflow runtime is never modified and never learns that notifications exist.
/// </summary>
public sealed class WorkflowNotificationSubscriber : IWorkflowEventSink
{
    private readonly NotificationEngine _engine;
    private readonly NotificationEngineOptions _options;

    /// <summary>Initializes a new instance of the <see cref="WorkflowNotificationSubscriber"/> class.</summary>
    /// <param name="engine">The notification engine.</param>
    /// <param name="options">The engine options.</param>
    public WorkflowNotificationSubscriber(NotificationEngine engine, NotificationEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(options);
        _engine = engine;
        _options = options;
    }

    /// <inheritdoc />
    public void Publish(WorkflowEvent workflowEvent)
    {
        ArgumentNullException.ThrowIfNull(workflowEvent);
        if (!_options.SubscribeToEngineEvents)
        {
            return;
        }

        switch (workflowEvent)
        {
            case WorkflowCompleted completed:
                Notify(completed.Tenant, completed.InstanceId, NotificationCategory.Workflow, NotificationPriority.Normal,
                    "Workflow completed", $"Workflow instance {completed.InstanceId} completed.");
                break;
            case WorkflowFailed failed:
                Notify(failed.Tenant, failed.InstanceId, NotificationCategory.Alert, NotificationPriority.High,
                    "Workflow failed", $"Workflow instance {failed.InstanceId} failed: {failed.Reason}");
                break;
            default:
                break;
        }
    }

    private void Notify(
        string tenant,
        Guid instanceId,
        NotificationCategory category,
        NotificationPriority priority,
        string subject,
        string body) =>
        _engine.Notify(
            new NotificationRequest
            {
                Category = category,
                Source = "workflow",
                SourceId = instanceId,
                Priority = priority,
                Subject = subject,
                Body = body,
            },
            new NotificationContext(tenant));
}
