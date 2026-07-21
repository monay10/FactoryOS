using FactoryOS.Plugins.Workflow.Notifications.Domain;

namespace FactoryOS.Plugins.Workflow.Notifications.Channels;

/// <summary>
/// Delivers notifications to connected clients over a real-time SignalR hub. A valid address is the recipient's
/// user or connection id, which every recipient has, so the SignalR channel is always deliverable.
/// </summary>
public sealed class SignalRChannelSender : NotificationChannelSenderBase
{
    /// <summary>Initializes a new instance of the <see cref="SignalRChannelSender"/> class.</summary>
    /// <param name="outbox">The outbox delivered messages are recorded to.</param>
    public SignalRChannelSender(INotificationOutbox outbox)
        : base(outbox)
    {
    }

    /// <inheritdoc />
    public override NotificationChannel Channel => NotificationChannel.SignalR;

    /// <inheritdoc />
    protected override bool IsValidAddress(string address) => true;
}
