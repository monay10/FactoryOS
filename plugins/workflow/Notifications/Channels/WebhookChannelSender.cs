using FactoryOS.Plugins.Workflow.Notifications.Domain;

namespace FactoryOS.Plugins.Workflow.Notifications.Channels;

/// <summary>Delivers notifications by POSTing to an outbound webhook. A valid address is an absolute HTTP(S) URL.</summary>
public sealed class WebhookChannelSender : NotificationChannelSenderBase
{
    /// <summary>Initializes a new instance of the <see cref="WebhookChannelSender"/> class.</summary>
    /// <param name="outbox">The outbox delivered messages are recorded to.</param>
    public WebhookChannelSender(INotificationOutbox outbox)
        : base(outbox)
    {
    }

    /// <inheritdoc />
    public override NotificationChannel Channel => NotificationChannel.Webhook;

    /// <inheritdoc />
    protected override bool IsValidAddress(string address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
