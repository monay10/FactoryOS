using FactoryOS.Plugins.Workflow.Notifications.Domain;

namespace FactoryOS.Plugins.Workflow.Notifications.Channels;

/// <summary>
/// Delivers notifications to the recipient's in-application inbox. A valid address is the recipient's user id,
/// which every recipient has, so the in-app channel is always deliverable.
/// </summary>
public sealed class InAppChannelSender : NotificationChannelSenderBase
{
    /// <summary>Initializes a new instance of the <see cref="InAppChannelSender"/> class.</summary>
    /// <param name="outbox">The outbox delivered messages are recorded to.</param>
    public InAppChannelSender(INotificationOutbox outbox)
        : base(outbox)
    {
    }

    /// <inheritdoc />
    public override NotificationChannel Channel => NotificationChannel.InApp;

    /// <inheritdoc />
    protected override bool IsValidAddress(string address) => true;
}
