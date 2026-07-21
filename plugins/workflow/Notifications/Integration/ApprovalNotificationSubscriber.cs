using FactoryOS.Plugins.Workflow.Approvals.Events;
using FactoryOS.Plugins.Workflow.Notifications.Configuration;
using FactoryOS.Plugins.Workflow.Notifications.Domain;
using FactoryOS.Plugins.Workflow.Notifications.Execution;

namespace FactoryOS.Plugins.Workflow.Notifications.Integration;

/// <summary>
/// Subscribes to the approval engine's events (by standing in as its <see cref="IApprovalEventSink"/>) and turns
/// them into notifications: a started approval notifies the <see cref="NotificationCategory.Approval"/>
/// subscribers, an assigned or escalated approval notifies its (new) approver directly. It reads the approval
/// engine's public events only; the approval engine is never modified and never learns notifications exist.
/// </summary>
public sealed class ApprovalNotificationSubscriber : IApprovalEventSink
{
    private readonly NotificationEngine _engine;
    private readonly NotificationEngineOptions _options;

    /// <summary>Initializes a new instance of the <see cref="ApprovalNotificationSubscriber"/> class.</summary>
    /// <param name="engine">The notification engine.</param>
    /// <param name="options">The engine options.</param>
    public ApprovalNotificationSubscriber(NotificationEngine engine, NotificationEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(options);
        _engine = engine;
        _options = options;
    }

    /// <inheritdoc />
    public void Publish(ApprovalEvent approvalEvent)
    {
        ArgumentNullException.ThrowIfNull(approvalEvent);
        if (!_options.SubscribeToEngineEvents)
        {
            return;
        }

        switch (approvalEvent)
        {
            case ApprovalStarted started:
                _engine.Notify(
                    new NotificationRequest
                    {
                        Category = NotificationCategory.Approval,
                        Source = "approval",
                        SourceKey = started.DefinitionKey,
                        SourceId = started.ApprovalId,
                        Subject = "Approval requested",
                        Body = $"Approval '{started.DefinitionKey}' started.",
                    },
                    new NotificationContext(started.Tenant));
                break;
            case ApprovalAssigned { Assignee: { Length: > 0 } assignee } assigned:
                NotifyApprover(assigned.Tenant, assigned.ApprovalId, assigned.DefinitionKey, assignee,
                    NotificationCategory.Approval, NotificationPriority.Normal, "Approval assigned",
                    $"Approval '{assigned.DefinitionKey}' awaits your decision.");
                break;
            case ApprovalEscalated { Assignee: { Length: > 0 } assignee } escalated:
                NotifyApprover(escalated.Tenant, escalated.ApprovalId, escalated.DefinitionKey, assignee,
                    NotificationCategory.Escalation, NotificationPriority.High, "Approval escalated",
                    $"Approval '{escalated.DefinitionKey}' was escalated to you.");
                break;
            default:
                break;
        }
    }

    private void NotifyApprover(
        string tenant,
        Guid approvalId,
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
                Source = "approval",
                SourceKey = definitionKey,
                SourceId = approvalId,
                Priority = priority,
                Recipients = [NotificationAssignment.ToUser(assignee)],
                Subject = subject,
                Body = body,
            },
            new NotificationContext(tenant));
}
