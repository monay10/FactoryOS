namespace FactoryOS.Plugins.Workflow.Notifications.Domain;

/// <summary>
/// A rolled-up group of notifications for one recipient on one channel, produced when notifications use the
/// <see cref="NotificationDeliveryPolicy.Digest"/> or <see cref="NotificationDeliveryPolicy.Batch"/> policy.
/// The runtime folds the members into a single delivered notification and suppresses the originals, so a busy
/// recipient gets one message instead of many.
/// </summary>
public sealed class NotificationBatch
{
    private readonly List<Notification> _members = [];

    /// <summary>Initializes a new instance of the <see cref="NotificationBatch"/> class.</summary>
    /// <param name="tenant">The owning tenant.</param>
    /// <param name="recipientUserId">The recipient the batch is for.</param>
    /// <param name="channel">The channel the batch delivers on.</param>
    public NotificationBatch(string tenant, string recipientUserId, NotificationChannel channel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientUserId);
        Tenant = tenant;
        RecipientUserId = recipientUserId;
        Channel = channel;
    }

    /// <summary>Gets the owning tenant.</summary>
    public string Tenant { get; }

    /// <summary>Gets the recipient the batch is for.</summary>
    public string RecipientUserId { get; }

    /// <summary>Gets the channel the batch delivers on.</summary>
    public NotificationChannel Channel { get; }

    /// <summary>Gets the notifications folded into the batch.</summary>
    public IReadOnlyList<Notification> Members => _members;

    /// <summary>Adds a notification to the batch.</summary>
    /// <param name="notification">The notification to add.</param>
    public void Add(Notification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        _members.Add(notification);
    }

    /// <summary>Composes the digest body from the members' subjects and bodies.</summary>
    /// <returns>The combined digest body.</returns>
    public string ComposeBody()
    {
        var lines = _members.Select(member =>
            string.IsNullOrWhiteSpace(member.Subject) ? member.Body : $"{member.Subject}: {member.Body}");
        return string.Join(Environment.NewLine, lines);
    }
}
