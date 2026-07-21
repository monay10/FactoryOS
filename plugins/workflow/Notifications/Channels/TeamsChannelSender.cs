using FactoryOS.Plugins.Workflow.Notifications.Domain;

namespace FactoryOS.Plugins.Workflow.Notifications.Channels;

/// <summary>
/// Delivers notifications to Microsoft Teams. A valid address is either an incoming-webhook URL or a
/// channel / conversation id.
/// </summary>
public sealed class TeamsChannelSender : NotificationChannelSenderBase
{
    /// <summary>Initializes a new instance of the <see cref="TeamsChannelSender"/> class.</summary>
    /// <param name="outbox">The outbox delivered messages are recorded to.</param>
    public TeamsChannelSender(INotificationOutbox outbox)
        : base(outbox)
    {
    }

    /// <inheritdoc />
    public override NotificationChannel Channel => NotificationChannel.Teams;

    /// <inheritdoc />
    protected override bool IsValidAddress(string address) =>
        Uri.TryCreate(address, UriKind.Absolute, out _) || address.Contains(':', StringComparison.Ordinal);
}
