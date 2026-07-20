namespace FactoryOS.Plugins.Notification;

/// <summary>
/// Configuration for the Notification module. Channel-to-transport routing is data, never a customer branch: a
/// factory decides that its <c>ops</c> channel goes to <c>sms</c> and <c>procurement</c> to <c>email</c> purely
/// by configuration. Unmapped channels fall back to <see cref="DefaultTransport"/>.
/// </summary>
public sealed record NotificationOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Modules:Notification";

    /// <summary>Maps a logical channel (for example <c>ops</c>) to a transport (for example <c>sms</c>).</summary>
    public IReadOnlyDictionary<string, string> ChannelTransports { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The transport used when a channel has no explicit mapping.</summary>
    public string DefaultTransport { get; init; } = "log";

    /// <summary>The logical channel that report-ready notifications are routed on (via <see cref="ChannelTransports"/>).</summary>
    public string ReportNotificationChannel { get; init; } = "reports";

    /// <summary>The priority stamped on report-ready notifications.</summary>
    public string ReportNotificationPriority { get; init; } = "Normal";

    /// <summary>The action stamped on report-ready notifications.</summary>
    public string ReportNotificationAction { get; init; } = "Notify";

    /// <summary>The logical channel that Company Brain answer notifications are routed on (via <see cref="ChannelTransports"/>).</summary>
    public string AssistantNotificationChannel { get; init; } = "assistant";

    /// <summary>The priority stamped on assistant-answer notifications.</summary>
    public string AssistantNotificationPriority { get; init; } = "Normal";

    /// <summary>The action stamped on assistant-answer notifications.</summary>
    public string AssistantNotificationAction { get; init; } = "Notify";
}
