namespace FactoryOS.Plugins.Workflow.Notifications.Domain;

/// <summary>The transport a notification is delivered over.</summary>
public enum NotificationChannel
{
    /// <summary>Delivered as an e-mail.</summary>
    Email = 0,

    /// <summary>Delivered as a text message.</summary>
    Sms = 1,

    /// <summary>Delivered as a mobile push notification.</summary>
    Push = 2,

    /// <summary>Delivered to a Microsoft Teams channel or user.</summary>
    Teams = 3,

    /// <summary>Delivered to a Slack channel or user.</summary>
    Slack = 4,

    /// <summary>Delivered by POSTing to an outbound webhook.</summary>
    Webhook = 5,

    /// <summary>Delivered to the recipient's in-application inbox.</summary>
    InApp = 6,

    /// <summary>Delivered to connected clients over a real-time SignalR hub.</summary>
    SignalR = 7,
}

/// <summary>How urgent a notification is; higher priorities are dispatched ahead of lower ones.</summary>
public enum NotificationPriority
{
    /// <summary>Informational; can be batched or digested.</summary>
    Low = 0,

    /// <summary>The default priority.</summary>
    Normal = 1,

    /// <summary>Important; delivered ahead of normal traffic.</summary>
    High = 2,

    /// <summary>Time-sensitive; delivered immediately.</summary>
    Urgent = 3,

    /// <summary>Highest priority; never suppressed by quiet hours or digests.</summary>
    Critical = 4,
}

/// <summary>What a notification is about — used for routing, subscriptions and preferences.</summary>
public enum NotificationCategory
{
    /// <summary>Raised by the workflow runtime.</summary>
    Workflow = 0,

    /// <summary>Raised by the human task engine.</summary>
    HumanTask = 1,

    /// <summary>Raised by the approval engine.</summary>
    Approval = 2,

    /// <summary>Raised by the forms engine.</summary>
    Form = 3,

    /// <summary>A system or platform message.</summary>
    System = 4,

    /// <summary>An alert that needs attention.</summary>
    Alert = 5,

    /// <summary>A reminder for an outstanding item.</summary>
    Reminder = 6,

    /// <summary>An escalation notice.</summary>
    Escalation = 7,

    /// <summary>A rolled-up digest of several notifications.</summary>
    Digest = 8,

    /// <summary>An uncategorized, general-purpose notification.</summary>
    General = 9,
}

/// <summary>The lifecycle state of a single notification.</summary>
public enum NotificationStatus
{
    /// <summary>Created and waiting in the queue for its scheduled time.</summary>
    Queued = 0,

    /// <summary>Being handed to its channel for delivery.</summary>
    Sending = 1,

    /// <summary>Accepted by the channel provider.</summary>
    Sent = 2,

    /// <summary>Confirmed delivered to the recipient.</summary>
    Delivered = 3,

    /// <summary>Opened / read by the recipient.</summary>
    Read = 4,

    /// <summary>A delivery attempt failed and a retry is scheduled.</summary>
    Retrying = 5,

    /// <summary>Retries are exhausted; the notification was moved to the dead-letter queue.</summary>
    DeadLettered = 6,

    /// <summary>Cancelled before delivery.</summary>
    Cancelled = 7,

    /// <summary>Its time-to-live passed before it could be delivered.</summary>
    Expired = 8,

    /// <summary>Suppressed by a rule, preference or because it was folded into a digest.</summary>
    Suppressed = 9,
}

/// <summary>When a notification should be delivered.</summary>
public enum NotificationDeliveryPolicy
{
    /// <summary>Delivered as soon as it is created.</summary>
    Immediate = 0,

    /// <summary>Delivered at an explicit scheduled time.</summary>
    Scheduled = 1,

    /// <summary>Delivered after a fixed delay from creation.</summary>
    Delayed = 2,

    /// <summary>Held and rolled up into a periodic digest for the recipient.</summary>
    Digest = 3,

    /// <summary>Held and delivered together with others as a batch.</summary>
    Batch = 4,
}

/// <summary>How a recipient assignment is interpreted.</summary>
public enum NotificationRecipientKind
{
    /// <summary>A specific user.</summary>
    User = 0,

    /// <summary>Every holder of a role.</summary>
    Role = 1,

    /// <summary>Every member of a group.</summary>
    Group = 2,

    /// <summary>A principal resolved at runtime from an expression over context values.</summary>
    Dynamic = 3,
}

/// <summary>Why a delivery attempt failed.</summary>
public enum NotificationFailureReason
{
    /// <summary>No sender is registered for the target channel.</summary>
    ChannelUnavailable = 0,

    /// <summary>The recipient has no address for the target channel.</summary>
    MissingAddress = 1,

    /// <summary>The channel provider rejected or errored on the message.</summary>
    TransportError = 2,

    /// <summary>The provider explicitly rejected the message (e.g. invalid recipient).</summary>
    Rejected = 3,

    /// <summary>The delivery attempt timed out.</summary>
    Timeout = 4,

    /// <summary>Delivery was suppressed by preference or quiet hours.</summary>
    Suppressed = 5,
}

/// <summary>The action recorded on a notification's audit history.</summary>
public enum NotificationHistoryAction
{
    /// <summary>The notification was queued.</summary>
    Queued = 0,

    /// <summary>A delivery attempt started.</summary>
    Sending = 1,

    /// <summary>The channel accepted the message.</summary>
    Sent = 2,

    /// <summary>The message was delivered.</summary>
    Delivered = 3,

    /// <summary>The recipient read the message.</summary>
    Read = 4,

    /// <summary>A delivery attempt failed.</summary>
    Failed = 5,

    /// <summary>A retry was scheduled.</summary>
    Retried = 6,

    /// <summary>The notification was dead-lettered.</summary>
    DeadLettered = 7,

    /// <summary>The notification was cancelled.</summary>
    Cancelled = 8,

    /// <summary>The notification expired.</summary>
    Expired = 9,

    /// <summary>The notification was suppressed.</summary>
    Suppressed = 10,
}
