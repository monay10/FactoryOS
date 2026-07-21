using FactoryOS.Plugins.Workflow.Notifications.Domain;

namespace FactoryOS.Plugins.Workflow.Notifications.Channels;

/// <summary>Delivers notifications as SMS. A valid address is a phone number (digits, optionally <c>+</c>-prefixed).</summary>
public sealed class SmsChannelSender : NotificationChannelSenderBase
{
    /// <summary>Initializes a new instance of the <see cref="SmsChannelSender"/> class.</summary>
    /// <param name="outbox">The outbox delivered messages are recorded to.</param>
    public SmsChannelSender(INotificationOutbox outbox)
        : base(outbox)
    {
    }

    /// <inheritdoc />
    public override NotificationChannel Channel => NotificationChannel.Sms;

    /// <inheritdoc />
    protected override bool IsValidAddress(string address)
    {
        var digits = address.StartsWith('+') ? address[1..] : address;
        return digits.Length >= 7 && digits.All(character => char.IsDigit(character) || character is ' ' or '-');
    }
}
