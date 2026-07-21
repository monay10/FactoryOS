using FactoryOS.Plugins.Workflow.Notifications.Domain;

namespace FactoryOS.Plugins.Workflow.Notifications.Channels;

/// <summary>Delivers notifications as e-mail. A valid address is a plausible mailbox (contains <c>@</c> and a dot).</summary>
public sealed class EmailChannelSender : NotificationChannelSenderBase
{
    /// <summary>Initializes a new instance of the <see cref="EmailChannelSender"/> class.</summary>
    /// <param name="outbox">The outbox delivered messages are recorded to.</param>
    public EmailChannelSender(INotificationOutbox outbox)
        : base(outbox)
    {
    }

    /// <inheritdoc />
    public override NotificationChannel Channel => NotificationChannel.Email;

    /// <inheritdoc />
    protected override bool IsValidAddress(string address)
    {
        var at = address.IndexOf('@', StringComparison.Ordinal);
        return at > 0 && address.IndexOf('.', at) > at + 1;
    }
}
