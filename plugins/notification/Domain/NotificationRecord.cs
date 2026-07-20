namespace FactoryOS.Plugins.Notification.Domain;

/// <summary>
/// One dispatched notification as retained in the outbox — the audit and read-model record of intent. The
/// transport connector actually delivers it; this is the durable trace that it was routed.
/// </summary>
/// <param name="Channel">The logical channel the action targeted.</param>
/// <param name="Transport">The transport the channel routed to.</param>
/// <param name="Priority">The notification priority.</param>
/// <param name="Subject">A human-readable description of the notification.</param>
/// <param name="Action">The action the notification fulfils.</param>
/// <param name="DispatchedAt">When the notification was dispatched.</param>
public readonly record struct NotificationRecord(
    string Channel,
    string Transport,
    string Priority,
    string Subject,
    string Action,
    DateTimeOffset DispatchedAt);
