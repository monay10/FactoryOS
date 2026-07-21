namespace FactoryOS.Plugins.Workflow.Notifications.Domain;

/// <summary>
/// The reusable blueprint for a notification: its category and default priority, the channels it goes out on,
/// the template it renders from, who receives it, the rules that adapt it to the payload, and its delivery and
/// retry policies. A definition is data — the runtime turns it, plus a context, into concrete notifications.
/// Build one with <see cref="Create"/> and the fluent builder.
/// </summary>
public sealed class NotificationDefinition
{
    /// <summary>Initializes a new instance of the <see cref="NotificationDefinition"/> class.</summary>
    /// <param name="key">The definition key.</param>
    /// <param name="name">The human-readable name.</param>
    /// <param name="category">The category.</param>
    /// <param name="defaultPriority">The default priority.</param>
    /// <param name="templateKey">The template key used to render the message.</param>
    /// <param name="channels">The channels the notification goes out on.</param>
    /// <param name="recipients">The recipient assignments.</param>
    /// <param name="rules">The rules that adapt the notification.</param>
    /// <param name="deliveryPolicy">When to deliver.</param>
    /// <param name="retry">The retry budget.</param>
    /// <param name="timeToLive">How long the notification stays deliverable, if limited.</param>
    /// <param name="sourceKey">The source key stamped on produced notifications, if any.</param>
    internal NotificationDefinition(
        string key,
        string name,
        NotificationCategory category,
        NotificationPriority defaultPriority,
        string templateKey,
        IReadOnlyList<NotificationChannel> channels,
        IReadOnlyList<NotificationAssignment> recipients,
        IReadOnlyList<NotificationRule> rules,
        NotificationDeliveryPolicy deliveryPolicy,
        NotificationRetryPolicy retry,
        TimeSpan? timeToLive,
        string? sourceKey)
    {
        Key = key;
        Name = name;
        Category = category;
        DefaultPriority = defaultPriority;
        TemplateKey = templateKey;
        Channels = channels;
        Recipients = recipients;
        Rules = rules;
        DeliveryPolicy = deliveryPolicy;
        Retry = retry;
        TimeToLive = timeToLive;
        SourceKey = sourceKey;
    }

    /// <summary>Gets the definition key.</summary>
    public string Key { get; }

    /// <summary>Gets the human-readable name.</summary>
    public string Name { get; }

    /// <summary>Gets the category.</summary>
    public NotificationCategory Category { get; }

    /// <summary>Gets the default priority.</summary>
    public NotificationPriority DefaultPriority { get; }

    /// <summary>Gets the template key used to render the message.</summary>
    public string TemplateKey { get; }

    /// <summary>Gets the channels the notification goes out on.</summary>
    public IReadOnlyList<NotificationChannel> Channels { get; }

    /// <summary>Gets the recipient assignments.</summary>
    public IReadOnlyList<NotificationAssignment> Recipients { get; }

    /// <summary>Gets the rules that adapt the notification.</summary>
    public IReadOnlyList<NotificationRule> Rules { get; }

    /// <summary>Gets when to deliver.</summary>
    public NotificationDeliveryPolicy DeliveryPolicy { get; }

    /// <summary>Gets the retry budget.</summary>
    public NotificationRetryPolicy Retry { get; }

    /// <summary>Gets how long the notification stays deliverable, if limited.</summary>
    public TimeSpan? TimeToLive { get; }

    /// <summary>Gets the source key stamped on produced notifications, if any.</summary>
    public string? SourceKey { get; }

    /// <summary>Starts building a notification definition.</summary>
    /// <param name="key">The definition key.</param>
    /// <param name="name">The human-readable name.</param>
    /// <returns>A fluent builder.</returns>
    public static NotificationDefinitionBuilder Create(string key, string name) => new(key, name);
}

/// <summary>A fluent builder for <see cref="NotificationDefinition"/>.</summary>
public sealed class NotificationDefinitionBuilder
{
    private readonly string _key;
    private readonly string _name;
    private readonly List<NotificationChannel> _channels = [];
    private readonly List<NotificationAssignment> _recipients = [];
    private readonly List<NotificationRule> _rules = [];
    private NotificationCategory _category = NotificationCategory.General;
    private NotificationPriority _priority = NotificationPriority.Normal;
    private string? _templateKey;
    private NotificationDeliveryPolicy _deliveryPolicy = NotificationDeliveryPolicy.Immediate;
    private NotificationRetryPolicy _retry = new(3, TimeSpan.FromMinutes(1));
    private TimeSpan? _timeToLive;
    private string? _sourceKey;

    /// <summary>Initializes a new instance of the <see cref="NotificationDefinitionBuilder"/> class.</summary>
    /// <param name="key">The definition key.</param>
    /// <param name="name">The human-readable name.</param>
    public NotificationDefinitionBuilder(string key, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _key = key;
        _name = name;
    }

    /// <summary>Sets the category.</summary>
    /// <param name="category">The category.</param>
    /// <returns>The same builder.</returns>
    public NotificationDefinitionBuilder InCategory(NotificationCategory category)
    {
        _category = category;
        return this;
    }

    /// <summary>Sets the default priority.</summary>
    /// <param name="priority">The priority.</param>
    /// <returns>The same builder.</returns>
    public NotificationDefinitionBuilder WithPriority(NotificationPriority priority)
    {
        _priority = priority;
        return this;
    }

    /// <summary>Sets the template key used to render the message.</summary>
    /// <param name="templateKey">The template key.</param>
    /// <returns>The same builder.</returns>
    public NotificationDefinitionBuilder UsingTemplate(string templateKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateKey);
        _templateKey = templateKey;
        return this;
    }

    /// <summary>Adds a delivery channel.</summary>
    /// <param name="channel">The channel.</param>
    /// <returns>The same builder.</returns>
    public NotificationDefinitionBuilder OnChannel(NotificationChannel channel)
    {
        if (!_channels.Contains(channel))
        {
            _channels.Add(channel);
        }

        return this;
    }

    /// <summary>Adds a recipient assignment.</summary>
    /// <param name="assignment">The assignment.</param>
    /// <returns>The same builder.</returns>
    public NotificationDefinitionBuilder ToRecipient(NotificationAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        _recipients.Add(assignment);
        return this;
    }

    /// <summary>Adds an adaptation rule.</summary>
    /// <param name="rule">The rule.</param>
    /// <returns>The same builder.</returns>
    public NotificationDefinitionBuilder AddRule(NotificationRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _rules.Add(rule);
        return this;
    }

    /// <summary>Sets the delivery policy.</summary>
    /// <param name="policy">The policy.</param>
    /// <returns>The same builder.</returns>
    public NotificationDefinitionBuilder Deliver(NotificationDeliveryPolicy policy)
    {
        _deliveryPolicy = policy;
        return this;
    }

    /// <summary>Sets the retry budget.</summary>
    /// <param name="maxAttempts">The total number of attempts.</param>
    /// <param name="backoff">The base back-off between attempts.</param>
    /// <returns>The same builder.</returns>
    public NotificationDefinitionBuilder WithRetry(int maxAttempts, TimeSpan backoff)
    {
        if (maxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "At least one attempt is required.");
        }

        _retry = new NotificationRetryPolicy(maxAttempts, backoff);
        return this;
    }

    /// <summary>Sets a time-to-live after which an undelivered notification expires.</summary>
    /// <param name="timeToLive">The time-to-live.</param>
    /// <returns>The same builder.</returns>
    public NotificationDefinitionBuilder ExpiresAfter(TimeSpan timeToLive)
    {
        _timeToLive = timeToLive;
        return this;
    }

    /// <summary>Narrows produced notifications to a source key (matched by subscriptions).</summary>
    /// <param name="sourceKey">The source key.</param>
    /// <returns>The same builder.</returns>
    public NotificationDefinitionBuilder ForSource(string sourceKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceKey);
        _sourceKey = sourceKey;
        return this;
    }

    /// <summary>Builds the definition.</summary>
    /// <returns>The built <see cref="NotificationDefinition"/>.</returns>
    public NotificationDefinition Build()
    {
        if (_channels.Count == 0)
        {
            _channels.Add(NotificationChannel.InApp);
        }

        return new NotificationDefinition(
            _key,
            _name,
            _category,
            _priority,
            _templateKey ?? _key,
            _channels,
            _recipients,
            _rules,
            _deliveryPolicy,
            _retry,
            _timeToLive,
            _sourceKey);
    }
}
