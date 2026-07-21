namespace FactoryOS.Plugins.Workflow.Notifications.Domain;

/// <summary>
/// A standing subscription: a user asks to be notified about a category (optionally narrowed to a specific
/// source key, such as one workflow definition) on a chosen set of channels. Subscriptions are how a
/// notification produced from a source event finds interested recipients beyond those the event names directly.
/// </summary>
public sealed class NotificationSubscription
{
    private readonly HashSet<NotificationChannel> _channels;

    /// <summary>Initializes a new instance of the <see cref="NotificationSubscription"/> class.</summary>
    /// <param name="userId">The subscribing user.</param>
    /// <param name="category">The category subscribed to.</param>
    /// <param name="channels">The channels to deliver on, or <see langword="null"/> for the in-app channel.</param>
    /// <param name="sourceKey">An optional source key to narrow the subscription to (e.g. a definition key).</param>
    public NotificationSubscription(
        string userId,
        NotificationCategory category,
        IReadOnlyCollection<NotificationChannel>? channels = null,
        string? sourceKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        UserId = userId;
        Category = category;
        _channels = channels is null || channels.Count == 0
            ? [NotificationChannel.InApp]
            : [.. channels];
        SourceKey = sourceKey;
    }

    /// <summary>Gets the subscribing user.</summary>
    public string UserId { get; }

    /// <summary>Gets the category subscribed to.</summary>
    public NotificationCategory Category { get; }

    /// <summary>Gets the source key the subscription is narrowed to, if any.</summary>
    public string? SourceKey { get; }

    /// <summary>Gets the channels to deliver on.</summary>
    public IReadOnlyCollection<NotificationChannel> Channels => _channels;

    /// <summary>Gets a value indicating whether the subscription matches a category and source key.</summary>
    /// <param name="category">The notification category.</param>
    /// <param name="sourceKey">The notification source key.</param>
    /// <returns><see langword="true"/> when the subscription applies.</returns>
    public bool Matches(NotificationCategory category, string? sourceKey)
    {
        if (Category != category)
        {
            return false;
        }

        return SourceKey is null || string.Equals(SourceKey, sourceKey, StringComparison.Ordinal);
    }
}
