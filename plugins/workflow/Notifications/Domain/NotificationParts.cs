namespace FactoryOS.Plugins.Workflow.Notifications.Domain;

/// <summary>
/// A file attached to a notification, referenced by URI rather than inlined, with the metadata a channel needs
/// to present or link it. The bytes live in object storage; the notification carries only the reference.
/// </summary>
public sealed class NotificationAttachment
{
    /// <summary>Initializes a new instance of the <see cref="NotificationAttachment"/> class.</summary>
    /// <param name="fileName">The display file name.</param>
    /// <param name="uri">The URI the content is fetched from.</param>
    /// <param name="contentType">The MIME content type.</param>
    /// <param name="sizeBytes">The content size in bytes, when known.</param>
    public NotificationAttachment(string fileName, string uri, string contentType, long sizeBytes = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        FileName = fileName;
        Uri = uri;
        ContentType = contentType;
        SizeBytes = sizeBytes;
    }

    /// <summary>Gets the display file name.</summary>
    public string FileName { get; }

    /// <summary>Gets the URI the content is fetched from.</summary>
    public string Uri { get; }

    /// <summary>Gets the MIME content type.</summary>
    public string ContentType { get; }

    /// <summary>Gets the content size in bytes, when known.</summary>
    public long SizeBytes { get; }
}

/// <summary>
/// The retry behaviour applied when a delivery attempt fails: how many attempts to make in total and the base
/// back-off between them (multiplied by the attempt number for a simple linear back-off). When the attempts are
/// exhausted the notification is dead-lettered.
/// </summary>
/// <param name="MaxAttempts">The total number of delivery attempts, including the first.</param>
/// <param name="Backoff">The base back-off between attempts.</param>
public sealed record NotificationRetryPolicy(int MaxAttempts, TimeSpan Backoff)
{
    /// <summary>A policy with no retries: a single attempt.</summary>
    public static NotificationRetryPolicy None { get; } = new(1, TimeSpan.Zero);

    /// <summary>Computes the back-off before the given (1-based) attempt number.</summary>
    /// <param name="attemptNumber">The 1-based number of the attempt about to be made.</param>
    /// <returns>The delay to wait before that attempt.</returns>
    public TimeSpan DelayBeforeAttempt(int attemptNumber) =>
        attemptNumber <= 1 ? TimeSpan.Zero : Backoff * (attemptNumber - 1);
}

/// <summary>A record of a delivery attempt that failed.</summary>
/// <param name="AttemptNumber">The 1-based attempt number that failed.</param>
/// <param name="Reason">Why it failed.</param>
/// <param name="Message">A human-readable failure message.</param>
/// <param name="OccurredOnUtc">When it failed.</param>
public sealed record NotificationFailure(
    int AttemptNumber, NotificationFailureReason Reason, string Message, DateTimeOffset OccurredOnUtc);

/// <summary>A record of a delivery attempt on a channel (successful or not).</summary>
/// <param name="AttemptNumber">The 1-based attempt number.</param>
/// <param name="Channel">The channel the attempt used.</param>
/// <param name="Address">The address the attempt targeted.</param>
/// <param name="Succeeded">Whether the attempt succeeded.</param>
/// <param name="ProviderMessageId">The provider's message id when the attempt succeeded.</param>
/// <param name="OccurredOnUtc">When the attempt was made.</param>
public sealed record NotificationDelivery(
    int AttemptNumber,
    NotificationChannel Channel,
    string Address,
    bool Succeeded,
    string? ProviderMessageId,
    DateTimeOffset OccurredOnUtc);
