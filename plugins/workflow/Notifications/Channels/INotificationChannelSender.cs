using System.Collections.Concurrent;
using FactoryOS.Plugins.Workflow.Notifications.Domain;

namespace FactoryOS.Plugins.Workflow.Notifications.Channels;

/// <summary>The message a channel sender delivers: the resolved address and rendered content for one channel.</summary>
/// <param name="NotificationId">The notification being delivered.</param>
/// <param name="Tenant">The owning tenant.</param>
/// <param name="Channel">The channel.</param>
/// <param name="Address">The resolved recipient address on the channel.</param>
/// <param name="Subject">The rendered subject, for channels that use one.</param>
/// <param name="Body">The rendered body.</param>
/// <param name="Priority">The notification priority.</param>
/// <param name="Attachments">The attachments to carry.</param>
public sealed record OutboundNotification(
    Guid NotificationId,
    string Tenant,
    NotificationChannel Channel,
    string Address,
    string? Subject,
    string Body,
    NotificationPriority Priority,
    IReadOnlyList<NotificationAttachment> Attachments);

/// <summary>The result of a channel send attempt.</summary>
/// <param name="Success">Whether the send succeeded.</param>
/// <param name="ProviderMessageId">The provider's message id, when the send succeeded.</param>
/// <param name="FailureReason">Why the send failed, when it did.</param>
/// <param name="Error">A human-readable error, when the send failed.</param>
public sealed record ChannelSendResult(
    bool Success, string? ProviderMessageId, NotificationFailureReason? FailureReason, string? Error)
{
    /// <summary>Creates a successful result.</summary>
    /// <param name="providerMessageId">The provider's message id.</param>
    /// <returns>The result.</returns>
    public static ChannelSendResult Ok(string providerMessageId) => new(true, providerMessageId, null, null);

    /// <summary>Creates a failed result.</summary>
    /// <param name="reason">Why the send failed.</param>
    /// <param name="error">A human-readable error.</param>
    /// <returns>The result.</returns>
    public static ChannelSendResult Fail(NotificationFailureReason reason, string error) =>
        new(false, null, reason, error);
}

/// <summary>
/// Delivers a notification over one transport (e-mail, SMS, push, Teams, Slack, webhook, in-app, SignalR). Each
/// implementation handles exactly one <see cref="NotificationChannel"/> and is the seam onto the real provider;
/// the default implementations format the message and write it to the in-process outbox.
/// </summary>
public interface INotificationChannelSender
{
    /// <summary>Gets the channel this sender delivers over.</summary>
    NotificationChannel Channel { get; }

    /// <summary>Delivers a message over the channel.</summary>
    /// <param name="message">The message to deliver.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The send result.</returns>
    Task<ChannelSendResult> SendAsync(OutboundNotification message, CancellationToken cancellationToken = default);
}

/// <summary>Records the messages the channel senders have delivered — the in-process delivery log / outbox.</summary>
public interface INotificationOutbox
{
    /// <summary>Records a delivered message.</summary>
    /// <param name="message">The delivered message.</param>
    void Record(OutboundNotification message);

    /// <summary>Gets the messages delivered on a channel, in order.</summary>
    /// <param name="channel">The channel.</param>
    /// <returns>The delivered messages.</returns>
    IReadOnlyList<OutboundNotification> ForChannel(NotificationChannel channel);

    /// <summary>Gets every delivered message, in order.</summary>
    /// <returns>The delivered messages.</returns>
    IReadOnlyList<OutboundNotification> All();
}

/// <summary>An in-memory <see cref="INotificationOutbox"/> that records delivered messages for inspection.</summary>
public sealed class InMemoryNotificationOutbox : INotificationOutbox
{
    private readonly ConcurrentQueue<OutboundNotification> _messages = new();

    /// <inheritdoc />
    public void Record(OutboundNotification message)
    {
        ArgumentNullException.ThrowIfNull(message);
        _messages.Enqueue(message);
    }

    /// <inheritdoc />
    public IReadOnlyList<OutboundNotification> ForChannel(NotificationChannel channel) =>
        _messages.Where(message => message.Channel == channel).ToArray();

    /// <inheritdoc />
    public IReadOnlyList<OutboundNotification> All() => _messages.ToArray();
}

/// <summary>
/// The shared base for the default channel senders: it validates that the message has an address shaped for the
/// channel, records the formatted message to the outbox, and returns a provider message id. A concrete sender
/// only declares its channel and how to recognise a valid address for it.
/// </summary>
public abstract class NotificationChannelSenderBase : INotificationChannelSender
{
    private readonly INotificationOutbox _outbox;

    /// <summary>Initializes a new instance of the <see cref="NotificationChannelSenderBase"/> class.</summary>
    /// <param name="outbox">The outbox delivered messages are recorded to.</param>
    protected NotificationChannelSenderBase(INotificationOutbox outbox)
    {
        ArgumentNullException.ThrowIfNull(outbox);
        _outbox = outbox;
    }

    /// <inheritdoc />
    public abstract NotificationChannel Channel { get; }

    /// <inheritdoc />
    public Task<ChannelSendResult> SendAsync(OutboundNotification message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(message.Address) || !IsValidAddress(message.Address))
        {
            return Task.FromResult(ChannelSendResult.Fail(
                NotificationFailureReason.MissingAddress,
                $"No valid {Channel} address for notification {message.NotificationId}."));
        }

        _outbox.Record(message);
        return Task.FromResult(ChannelSendResult.Ok($"{Channel}:{Guid.NewGuid():N}"));
    }

    /// <summary>Determines whether an address is shaped correctly for this channel.</summary>
    /// <param name="address">The address to validate.</param>
    /// <returns><see langword="true"/> when the address is valid for the channel.</returns>
    protected abstract bool IsValidAddress(string address);
}
