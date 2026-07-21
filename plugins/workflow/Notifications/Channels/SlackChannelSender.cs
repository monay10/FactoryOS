using FactoryOS.Plugins.Workflow.Notifications.Domain;

namespace FactoryOS.Plugins.Workflow.Notifications.Channels;

/// <summary>
/// Delivers notifications to Slack. A valid address is a channel (<c>#name</c>), a direct handle (<c>@user</c>),
/// a channel/user id or an incoming-webhook URL.
/// </summary>
public sealed class SlackChannelSender : NotificationChannelSenderBase
{
    /// <summary>Initializes a new instance of the <see cref="SlackChannelSender"/> class.</summary>
    /// <param name="outbox">The outbox delivered messages are recorded to.</param>
    public SlackChannelSender(INotificationOutbox outbox)
        : base(outbox)
    {
    }

    /// <inheritdoc />
    public override NotificationChannel Channel => NotificationChannel.Slack;

    /// <inheritdoc />
    protected override bool IsValidAddress(string address) =>
        address[0] is '#' or '@' or 'C' or 'U' || Uri.TryCreate(address, UriKind.Absolute, out _);
}
