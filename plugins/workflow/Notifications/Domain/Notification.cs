namespace FactoryOS.Plugins.Workflow.Notifications.Domain;

/// <summary>
/// A single notification instance — one message to one recipient on one channel. It carries the rendered
/// subject and body, the delivery schedule and retry budget, and its own lifecycle state (queued → sending →
/// sent → delivered → read, or retrying → dead-lettered, or cancelled / expired / suppressed). The runtime
/// drives the transitions; the instance enforces that only legal ones happen and keeps the per-attempt delivery
/// and failure log for audit. It never crosses tenants.
/// </summary>
public sealed class Notification
{
    private readonly List<NotificationDelivery> _deliveries = [];
    private readonly List<NotificationFailure> _failures = [];
    private readonly Dictionary<string, object?> _variables;

    /// <summary>Initializes a new instance of the <see cref="Notification"/> class.</summary>
    /// <param name="tenant">The owning tenant.</param>
    /// <param name="category">The category.</param>
    /// <param name="priority">The priority.</param>
    /// <param name="channel">The channel.</param>
    /// <param name="recipientUserId">The recipient user id.</param>
    /// <param name="recipientAddress">The recipient's address on the channel.</param>
    /// <param name="body">The rendered body.</param>
    /// <param name="createdOnUtc">When the notification was created.</param>
    /// <param name="subject">The rendered subject, if any.</param>
    /// <param name="deliveryPolicy">When to deliver.</param>
    /// <param name="retry">The retry budget.</param>
    /// <param name="scheduledForUtc">When the notification becomes due.</param>
    /// <param name="culture">The culture the text was rendered in.</param>
    /// <param name="definitionKey">The producing definition key, if any.</param>
    /// <param name="source">The producing source (e.g. <c>workflow</c>, <c>approval</c>, <c>adhoc</c>).</param>
    /// <param name="sourceKey">The source key (e.g. a definition key), if any.</param>
    /// <param name="sourceId">The source entity id, if any.</param>
    /// <param name="correlationId">The correlation id carried from the source event, if any.</param>
    /// <param name="expiresOnUtc">When the notification expires undelivered, if ever.</param>
    /// <param name="variables">The context values the notification was rendered from.</param>
    /// <param name="attachments">Attachments carried with the notification.</param>
    public Notification(
        string tenant,
        NotificationCategory category,
        NotificationPriority priority,
        NotificationChannel channel,
        string recipientUserId,
        string recipientAddress,
        string body,
        DateTimeOffset createdOnUtc,
        string? subject = null,
        NotificationDeliveryPolicy deliveryPolicy = NotificationDeliveryPolicy.Immediate,
        NotificationRetryPolicy? retry = null,
        DateTimeOffset? scheduledForUtc = null,
        string culture = "en",
        string? definitionKey = null,
        string source = "adhoc",
        string? sourceKey = null,
        Guid? sourceId = null,
        string? correlationId = null,
        DateTimeOffset? expiresOnUtc = null,
        IReadOnlyDictionary<string, object?>? variables = null,
        IReadOnlyCollection<NotificationAttachment>? attachments = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientUserId);
        ArgumentNullException.ThrowIfNull(recipientAddress);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        Id = Guid.NewGuid();
        Tenant = tenant;
        Category = category;
        Priority = priority;
        Channel = channel;
        RecipientUserId = recipientUserId;
        RecipientAddress = recipientAddress;
        Subject = subject;
        Body = body;
        CreatedOnUtc = createdOnUtc;
        DeliveryPolicy = deliveryPolicy;
        Retry = retry ?? NotificationRetryPolicy.None;
        ScheduledForUtc = scheduledForUtc ?? createdOnUtc;
        Culture = culture;
        DefinitionKey = definitionKey;
        Source = source;
        SourceKey = sourceKey;
        SourceId = sourceId;
        CorrelationId = correlationId;
        ExpiresOnUtc = expiresOnUtc;
        Status = NotificationStatus.Queued;
        _variables = variables is null
            ? new Dictionary<string, object?>(StringComparer.Ordinal)
            : new Dictionary<string, object?>(variables, StringComparer.Ordinal);
        Attachments = attachments is null ? [] : [.. attachments];
    }

    /// <summary>Gets the notification id.</summary>
    public Guid Id { get; }

    /// <summary>Gets the owning tenant.</summary>
    public string Tenant { get; }

    /// <summary>Gets the category.</summary>
    public NotificationCategory Category { get; }

    /// <summary>Gets the priority.</summary>
    public NotificationPriority Priority { get; private set; }

    /// <summary>Gets the channel.</summary>
    public NotificationChannel Channel { get; }

    /// <summary>Gets the recipient user id.</summary>
    public string RecipientUserId { get; }

    /// <summary>Gets the recipient's address on the channel.</summary>
    public string RecipientAddress { get; }

    /// <summary>Gets the rendered subject, if any.</summary>
    public string? Subject { get; }

    /// <summary>Gets the rendered body.</summary>
    public string Body { get; }

    /// <summary>Gets the culture the text was rendered in.</summary>
    public string Culture { get; }

    /// <summary>Gets when the notification was created.</summary>
    public DateTimeOffset CreatedOnUtc { get; }

    /// <summary>Gets when to deliver.</summary>
    public NotificationDeliveryPolicy DeliveryPolicy { get; }

    /// <summary>Gets the retry budget.</summary>
    public NotificationRetryPolicy Retry { get; }

    /// <summary>Gets when the notification becomes due for delivery.</summary>
    public DateTimeOffset ScheduledForUtc { get; private set; }

    /// <summary>Gets when the notification expires undelivered, if ever.</summary>
    public DateTimeOffset? ExpiresOnUtc { get; }

    /// <summary>Gets the producing definition key, if any.</summary>
    public string? DefinitionKey { get; }

    /// <summary>Gets the producing source.</summary>
    public string Source { get; }

    /// <summary>Gets the source key (e.g. a definition key), if any.</summary>
    public string? SourceKey { get; }

    /// <summary>Gets the source entity id, if any.</summary>
    public Guid? SourceId { get; }

    /// <summary>Gets the correlation id carried from the source event, if any.</summary>
    public string? CorrelationId { get; }

    /// <summary>Gets the current lifecycle status.</summary>
    public NotificationStatus Status { get; private set; }

    /// <summary>Gets how many delivery attempts have been made.</summary>
    public int Attempts { get; private set; }

    /// <summary>Gets when the last delivery attempt was made, if any.</summary>
    public DateTimeOffset? LastAttemptOnUtc { get; private set; }

    /// <summary>Gets when the notification was accepted by its channel, if it has been.</summary>
    public DateTimeOffset? SentOnUtc { get; private set; }

    /// <summary>Gets when the notification was delivered, if it has been.</summary>
    public DateTimeOffset? DeliveredOnUtc { get; private set; }

    /// <summary>Gets when the notification was read, if it has been.</summary>
    public DateTimeOffset? ReadOnUtc { get; private set; }

    /// <summary>Gets the provider's message id from the successful attempt, if any.</summary>
    public string? ProviderMessageId { get; private set; }

    /// <summary>Gets the attachments carried with the notification.</summary>
    public IReadOnlyList<NotificationAttachment> Attachments { get; }

    /// <summary>Gets the per-attempt delivery log.</summary>
    public IReadOnlyList<NotificationDelivery> Deliveries => _deliveries;

    /// <summary>Gets the failures recorded across attempts.</summary>
    public IReadOnlyList<NotificationFailure> Failures => _failures;

    /// <summary>Gets the context values the notification was rendered from.</summary>
    public IReadOnlyDictionary<string, object?> Variables => _variables;

    /// <summary>Gets a value indicating whether the notification is still waiting in the queue.</summary>
    public bool IsPending => Status is NotificationStatus.Queued or NotificationStatus.Retrying;

    /// <summary>Gets a value indicating whether the notification has reached a terminal, non-retryable state.</summary>
    public bool IsTerminal => Status is NotificationStatus.Delivered or NotificationStatus.Read
        or NotificationStatus.DeadLettered or NotificationStatus.Cancelled or NotificationStatus.Expired
        or NotificationStatus.Suppressed;

    /// <summary>
    /// Gets a value indicating whether the notification is due for delivery at the given time. Notifications
    /// held for a digest or batch are never due through the normal queue — they are released by a digest flush.
    /// </summary>
    /// <param name="nowUtc">The current time.</param>
    /// <returns><see langword="true"/> when pending, scheduled at or before now, and not awaiting a digest.</returns>
    public bool IsDue(DateTimeOffset nowUtc) =>
        IsPending
        && ScheduledForUtc <= nowUtc
        && DeliveryPolicy is not (NotificationDeliveryPolicy.Digest or NotificationDeliveryPolicy.Batch);

    /// <summary>Applies a rule's priority override before the notification is queued.</summary>
    /// <param name="priority">The new priority.</param>
    public void OverridePriority(NotificationPriority priority) => Priority = priority;

    /// <summary>Begins a delivery attempt, moving to <see cref="NotificationStatus.Sending"/>.</summary>
    /// <param name="nowUtc">The current time.</param>
    /// <returns>The 1-based number of the attempt just begun.</returns>
    public int BeginAttempt(DateTimeOffset nowUtc)
    {
        if (!IsPending)
        {
            throw new InvalidOperationException($"Notification {Id} is not pending (status {Status}).");
        }

        Attempts++;
        LastAttemptOnUtc = nowUtc;
        Status = NotificationStatus.Sending;
        return Attempts;
    }

    /// <summary>Marks the current attempt accepted by the channel.</summary>
    /// <param name="nowUtc">The current time.</param>
    /// <param name="providerMessageId">The provider's message id.</param>
    public void MarkSent(DateTimeOffset nowUtc, string? providerMessageId)
    {
        Status = NotificationStatus.Sent;
        SentOnUtc = nowUtc;
        ProviderMessageId = providerMessageId;
        _deliveries.Add(new NotificationDelivery(Attempts, Channel, RecipientAddress, true, providerMessageId, nowUtc));
    }

    /// <summary>Marks the notification delivered.</summary>
    /// <param name="nowUtc">The current time.</param>
    public void MarkDelivered(DateTimeOffset nowUtc)
    {
        Status = NotificationStatus.Delivered;
        DeliveredOnUtc = nowUtc;
    }

    /// <summary>Marks the notification read by its recipient.</summary>
    /// <param name="nowUtc">The current time.</param>
    /// <returns><see langword="true"/> when the transition was applied.</returns>
    public bool MarkRead(DateTimeOffset nowUtc)
    {
        if (Status is not (NotificationStatus.Sent or NotificationStatus.Delivered))
        {
            return false;
        }

        Status = NotificationStatus.Read;
        ReadOnUtc = nowUtc;
        return true;
    }

    /// <summary>Records a failed delivery attempt (does not itself schedule a retry or dead-letter).</summary>
    /// <param name="reason">Why it failed.</param>
    /// <param name="message">A human-readable message.</param>
    /// <param name="nowUtc">The current time.</param>
    public void RecordFailure(NotificationFailureReason reason, string message, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        _failures.Add(new NotificationFailure(Attempts, reason, message, nowUtc));
        _deliveries.Add(new NotificationDelivery(Attempts, Channel, RecipientAddress, false, null, nowUtc));
    }

    /// <summary>Schedules a retry at the given time, moving to <see cref="NotificationStatus.Retrying"/>.</summary>
    /// <param name="nextAttemptOnUtc">When the next attempt becomes due.</param>
    public void ScheduleRetry(DateTimeOffset nextAttemptOnUtc)
    {
        Status = NotificationStatus.Retrying;
        ScheduledForUtc = nextAttemptOnUtc;
    }

    /// <summary>Moves the notification to the dead-letter queue after its retries are exhausted.</summary>
    public void DeadLetter() => Status = NotificationStatus.DeadLettered;

    /// <summary>Requeues a dead-lettered notification for another attempt at the given time.</summary>
    /// <param name="nowUtc">When the requeued notification becomes due.</param>
    /// <returns><see langword="true"/> when the notification was dead-lettered and is now requeued.</returns>
    public bool Requeue(DateTimeOffset nowUtc)
    {
        if (Status != NotificationStatus.DeadLettered)
        {
            return false;
        }

        Status = NotificationStatus.Queued;
        ScheduledForUtc = nowUtc;
        return true;
    }

    /// <summary>Cancels the notification before delivery.</summary>
    /// <returns><see langword="true"/> when the notification was pending and is now cancelled.</returns>
    public bool Cancel()
    {
        if (!IsPending)
        {
            return false;
        }

        Status = NotificationStatus.Cancelled;
        return true;
    }

    /// <summary>Expires the notification because its time-to-live passed undelivered.</summary>
    /// <returns><see langword="true"/> when the notification was pending and is now expired.</returns>
    public bool Expire()
    {
        if (!IsPending)
        {
            return false;
        }

        Status = NotificationStatus.Expired;
        return true;
    }

    /// <summary>Suppresses the notification (by preference, quiet hours or folding into a digest).</summary>
    public void Suppress() => Status = NotificationStatus.Suppressed;
}
