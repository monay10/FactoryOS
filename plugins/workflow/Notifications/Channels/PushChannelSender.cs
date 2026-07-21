using FactoryOS.Plugins.Workflow.Notifications.Domain;

namespace FactoryOS.Plugins.Workflow.Notifications.Channels;

/// <summary>Delivers notifications as mobile push. A valid address is a device token of reasonable length.</summary>
public sealed class PushChannelSender : NotificationChannelSenderBase
{
    /// <summary>Initializes a new instance of the <see cref="PushChannelSender"/> class.</summary>
    /// <param name="outbox">The outbox delivered messages are recorded to.</param>
    public PushChannelSender(INotificationOutbox outbox)
        : base(outbox)
    {
    }

    /// <inheritdoc />
    public override NotificationChannel Channel => NotificationChannel.Push;

    /// <inheritdoc />
    protected override bool IsValidAddress(string address) => address.Trim().Length >= 8;
}
