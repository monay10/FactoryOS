namespace FactoryOS.Plugins.Workflow.Notifications.Domain;

/// <summary>A single, immutable entry in a notification's audit history.</summary>
/// <param name="NotificationId">The notification the entry belongs to.</param>
/// <param name="Action">What happened.</param>
/// <param name="Actor">Who or what caused it (a user id, or the engine component).</param>
/// <param name="Detail">An optional human-readable detail.</param>
/// <param name="OccurredOnUtc">When it happened.</param>
public sealed record NotificationHistoryEntry(
    Guid NotificationId,
    NotificationHistoryAction Action,
    string Actor,
    string? Detail,
    DateTimeOffset OccurredOnUtc);
