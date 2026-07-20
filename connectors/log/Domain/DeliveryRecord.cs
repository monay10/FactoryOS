namespace FactoryOS.Connectors.Log.Domain;

/// <summary>
/// One delivered notification as retained in the log journal — the audit trail and read-model of everything the
/// log transport carried out of the system.
/// </summary>
/// <param name="Channel">The logical channel the notification targeted.</param>
/// <param name="Priority">The notification priority.</param>
/// <param name="Subject">A human-readable description of the notification.</param>
/// <param name="Action">The action the notification fulfilled.</param>
/// <param name="OccurredAt">When the notification was raised.</param>
public readonly record struct DeliveryRecord(
    string Channel,
    string Priority,
    string Subject,
    string Action,
    DateTimeOffset OccurredAt);
