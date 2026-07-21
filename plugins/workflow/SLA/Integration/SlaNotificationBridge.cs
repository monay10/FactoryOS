using FactoryOS.Plugins.Workflow.Notifications.Domain;
using FactoryOS.Plugins.Workflow.Notifications.Integration;
using FactoryOS.Plugins.Workflow.SLA.Events;

namespace FactoryOS.Plugins.Workflow.SLA.Integration;

/// <summary>
/// Forwards the SLA events that need a human to look at them — a reminder, an escalation, a missed deadline, a
/// timeout — to the notification engine, through its existing <see cref="GenericEventSubscriber"/> seam. The
/// quiet lifecycle events (started, paused, resumed, completed, cancelled) are deliberately not forwarded: an
/// SLA that is simply running is not news, and turning every transition into a message trains people to ignore
/// them.
/// <para>
/// This bridge is <b>opt-in</b> and lives on the SLA side: the SLA engine core has no dependency on the
/// notification engine, and the notification engine is neither modified nor aware that SLAs exist. Because the
/// SLA event seam fans out to every registered sink, adding this bridge does not displace any other consumer.
/// </para>
/// </summary>
public sealed class SlaNotificationBridge : ISlaEventSink
{
    private readonly GenericEventSubscriber _notifications;

    /// <summary>Initializes a new instance of the <see cref="SlaNotificationBridge"/> class.</summary>
    /// <param name="notifications">The notification engine's generic event seam.</param>
    public SlaNotificationBridge(GenericEventSubscriber notifications)
    {
        ArgumentNullException.ThrowIfNull(notifications);
        _notifications = notifications;
    }

    /// <inheritdoc />
    public void Publish(SlaEvent slaEvent)
    {
        ArgumentNullException.ThrowIfNull(slaEvent);

        switch (slaEvent)
        {
            case SlaReminderTriggered reminder:
                _notifications.Handle(
                    reminder, reminder.Tenant, NotificationCategory.Reminder,
                    sourceKey: reminder.DefinitionKey);
                break;

            // An escalation names who it went to, so it is delivered to that person rather than broadcast.
            case SlaEscalated escalated:
                _notifications.Handle(
                    escalated, escalated.Tenant, NotificationCategory.Escalation,
                    [NotificationAssignment.ToUser(escalated.Assignee)],
                    escalated.DefinitionKey,
                    NotificationPriority.High);
                break;

            case SlaExpired expired:
                _notifications.Handle(
                    expired, expired.Tenant, NotificationCategory.Alert,
                    sourceKey: expired.DefinitionKey,
                    priority: NotificationPriority.High);
                break;

            case SlaTimedOut timedOut:
                _notifications.Handle(
                    timedOut, timedOut.Tenant, NotificationCategory.Alert,
                    sourceKey: timedOut.DefinitionKey,
                    priority: NotificationPriority.Critical);
                break;

            default:
                break;
        }
    }
}
