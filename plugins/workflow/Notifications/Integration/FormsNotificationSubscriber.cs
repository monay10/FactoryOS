using FactoryOS.Plugins.Forms.Engine.Events;
using FactoryOS.Plugins.Workflow.Notifications.Configuration;
using FactoryOS.Plugins.Workflow.Notifications.Domain;
using FactoryOS.Plugins.Workflow.Notifications.Execution;

namespace FactoryOS.Plugins.Workflow.Notifications.Integration;

/// <summary>
/// Subscribes to the forms engine's events (by standing in as its <see cref="IFormEventSink"/>) and turns them
/// into notifications: a submitted form notifies the <see cref="NotificationCategory.Form"/> subscribers, a
/// rejected form raises an alert. It reads the forms engine's public events only; the forms engine is never
/// modified and never learns that notifications exist.
/// </summary>
public sealed class FormsNotificationSubscriber : IFormEventSink
{
    private readonly NotificationEngine _engine;
    private readonly NotificationEngineOptions _options;

    /// <summary>Initializes a new instance of the <see cref="FormsNotificationSubscriber"/> class.</summary>
    /// <param name="engine">The notification engine.</param>
    /// <param name="options">The engine options.</param>
    public FormsNotificationSubscriber(NotificationEngine engine, NotificationEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(options);
        _engine = engine;
        _options = options;
    }

    /// <inheritdoc />
    public void Publish(FormEvent formEvent)
    {
        ArgumentNullException.ThrowIfNull(formEvent);
        if (!_options.SubscribeToEngineEvents)
        {
            return;
        }

        switch (formEvent)
        {
            case FormSubmitted submitted:
                Notify(submitted.Tenant, submitted.FormKey, submitted.InstanceId, NotificationCategory.Form,
                    NotificationPriority.Normal, "Form submitted", $"Form '{submitted.FormKey}' was submitted.");
                break;
            case FormRejected rejected:
                Notify(rejected.Tenant, rejected.FormKey, rejected.InstanceId, NotificationCategory.Alert,
                    NotificationPriority.High, "Form rejected",
                    $"Form '{rejected.FormKey}' was rejected: {rejected.Reason}");
                break;
            default:
                break;
        }
    }

    private void Notify(
        string tenant,
        string formKey,
        Guid instanceId,
        NotificationCategory category,
        NotificationPriority priority,
        string subject,
        string body) =>
        _engine.Notify(
            new NotificationRequest
            {
                Category = category,
                Source = "form",
                SourceKey = formKey,
                SourceId = instanceId,
                Priority = priority,
                Subject = subject,
                Body = body,
            },
            new NotificationContext(tenant));
}
