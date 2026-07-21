using FactoryOS.Plugins.Workflow.Notifications.Configuration;
using FactoryOS.Plugins.Workflow.Notifications.Domain;
using FactoryOS.Plugins.Workflow.Notifications.Persistence;

namespace FactoryOS.Plugins.Workflow.Notifications.Execution;

/// <summary>
/// A request to produce notifications: what it is about, who receives it (directly and via subscriptions), the
/// content (a template key, or an inline subject and body for ad-hoc messages), and the delivery and retry
/// behaviour. The runtime turns a request plus a context into concrete notifications through the router.
/// </summary>
public sealed record NotificationRequest
{
    /// <summary>Gets the category.</summary>
    public required NotificationCategory Category { get; init; }

    /// <summary>Gets the producing source (e.g. <c>workflow</c>, <c>approval</c>, <c>adhoc</c>).</summary>
    public string Source { get; init; } = "adhoc";

    /// <summary>Gets the source key (e.g. a definition key) used to match subscriptions.</summary>
    public string? SourceKey { get; init; }

    /// <summary>Gets the source entity id, if any.</summary>
    public Guid? SourceId { get; init; }

    /// <summary>
    /// Gets the explicit recipient assignments named by the caller; empty falls back to the producing
    /// definition's recipients (and, either way, matching subscribers are added).
    /// </summary>
    public IReadOnlyList<NotificationAssignment> Recipients { get; init; } = [];

    /// <summary>
    /// Gets the channels to deliver on; empty falls back to the producing definition's channels, then to the
    /// in-app channel.
    /// </summary>
    public IReadOnlyList<NotificationChannel> Channels { get; init; } = [];

    /// <summary>Gets the priority; omitted falls back to the producing definition's default, then to normal.</summary>
    public NotificationPriority? Priority { get; init; }

    /// <summary>Gets the template key to render from, if any.</summary>
    public string? TemplateKey { get; init; }

    /// <summary>Gets the inline subject used when no template is registered.</summary>
    public string? Subject { get; init; }

    /// <summary>Gets the inline body used when no template is registered.</summary>
    public string? Body { get; init; }

    /// <summary>
    /// Gets the delivery policy; omitted falls back to the producing definition's policy, then to immediate.
    /// </summary>
    public NotificationDeliveryPolicy? DeliveryPolicy { get; init; }

    /// <summary>Gets the retry budget; defaults to the engine's default when omitted.</summary>
    public NotificationRetryPolicy? Retry { get; init; }

    /// <summary>Gets the delay applied for <see cref="NotificationDeliveryPolicy.Delayed"/> delivery.</summary>
    public TimeSpan? Delay { get; init; }

    /// <summary>Gets the explicit due time for <see cref="NotificationDeliveryPolicy.Scheduled"/> delivery.</summary>
    public DateTimeOffset? ScheduledForUtc { get; init; }

    /// <summary>Gets the definition key that produced the request, if any.</summary>
    public string? DefinitionKey { get; init; }

    /// <summary>Gets the attachments to carry.</summary>
    public IReadOnlyList<NotificationAttachment> Attachments { get; init; } = [];
}

/// <summary>The notifications produced from a request: those to deliver, and those suppressed up front.</summary>
/// <param name="Deliverable">The notifications queued for delivery.</param>
/// <param name="Suppressed">The notifications suppressed by a rule or preference before queueing.</param>
public sealed record RoutedNotifications(
    IReadOnlyList<Notification> Deliverable, IReadOnlyList<Notification> Suppressed);

/// <summary>
/// Turns a <see cref="NotificationRequest"/> into concrete <see cref="Notification"/> instances: it resolves the
/// direct and subscribed recipients, applies the definition's rules to adapt priority / template / suppression,
/// filters channels through each recipient's preferences, renders the per-channel template (or the inline
/// content), and stamps the delivery schedule. It produces data only; queueing and dispatch happen elsewhere.
/// </summary>
public sealed class NotificationRouter
{
    private readonly RecipientResolver _recipients;
    private readonly PreferenceResolver _preferences;
    private readonly INotificationSubscriptionStore _subscriptions;
    private readonly INotificationTemplateRepository _templates;
    private readonly NotificationTemplateEngine _templateEngine;
    private readonly NotificationEngineOptions _options;

    /// <summary>Initializes a new instance of the <see cref="NotificationRouter"/> class.</summary>
    /// <param name="recipients">The recipient resolver.</param>
    /// <param name="preferences">The preference resolver.</param>
    /// <param name="subscriptions">The subscription store.</param>
    /// <param name="templates">The template repository.</param>
    /// <param name="templateEngine">The template engine.</param>
    /// <param name="options">The engine options.</param>
    public NotificationRouter(
        RecipientResolver recipients,
        PreferenceResolver preferences,
        INotificationSubscriptionStore subscriptions,
        INotificationTemplateRepository templates,
        NotificationTemplateEngine templateEngine,
        NotificationEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(recipients);
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(subscriptions);
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(templateEngine);
        ArgumentNullException.ThrowIfNull(options);
        _recipients = recipients;
        _preferences = preferences;
        _subscriptions = subscriptions;
        _templates = templates;
        _templateEngine = templateEngine;
        _options = options;
    }

    /// <summary>Builds the notifications for a request.</summary>
    /// <param name="request">The request.</param>
    /// <param name="definition">The producing definition, if any (supplies rules and defaults).</param>
    /// <param name="context">The context (tenant, culture, values, correlation).</param>
    /// <param name="nowUtc">The current time.</param>
    /// <returns>The routed notifications.</returns>
    public RoutedNotifications Build(
        NotificationRequest request,
        NotificationDefinition? definition,
        NotificationContext context,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        var deliverable = new List<Notification>();
        var suppressed = new List<Notification>();
        var rules = definition?.Rules ?? [];
        var effective = Resolve(request, definition);

        foreach (var (recipient, channels) in ResolveTargets(request, effective, context.Values))
        {
            var (ruleSuppress, priority, templateKey) =
                ApplyRules(rules, context.Values, effective.Priority, effective.TemplateKey);

            if (ruleSuppress)
            {
                suppressed.Add(Build(request, effective, context, recipient, channels[0], priority, templateKey, nowUtc));
                continue;
            }

            var allowed = _preferences.ResolveChannels(recipient.UserId, request.Category, priority, channels, nowUtc);
            if (allowed.Count == 0)
            {
                suppressed.Add(Build(request, effective, context, recipient, channels[0], priority, templateKey, nowUtc));
                continue;
            }

            foreach (var channel in allowed)
            {
                deliverable.Add(Build(request, effective, context, recipient, channel, priority, templateKey, nowUtc));
            }
        }

        return new RoutedNotifications(deliverable, suppressed);
    }

    /// <summary>Merges the request with its definition's defaults; the request always wins where it is explicit.</summary>
    private EffectiveSettings Resolve(NotificationRequest request, NotificationDefinition? definition)
    {
        var recipients = request.Recipients.Count > 0
            ? request.Recipients
            : definition?.Recipients ?? [];
        var channels = request.Channels.Count > 0
            ? request.Channels
            : definition?.Channels is { Count: > 0 } definitionChannels
                ? definitionChannels
                : [NotificationChannel.InApp];

        return new EffectiveSettings(
            recipients,
            channels,
            request.Priority ?? definition?.DefaultPriority ?? NotificationPriority.Normal,
            request.DeliveryPolicy ?? definition?.DeliveryPolicy ?? NotificationDeliveryPolicy.Immediate,
            request.TemplateKey ?? definition?.TemplateKey,
            request.Retry ?? definition?.Retry
                ?? new NotificationRetryPolicy(_options.DefaultMaxAttempts, _options.RetryBackoff),
            definition?.TimeToLive);
    }

    private IEnumerable<(NotificationRecipient Recipient, IReadOnlyList<NotificationChannel> Channels)> ResolveTargets(
        NotificationRequest request, EffectiveSettings effective, IReadOnlyDictionary<string, object?> values)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // Directly named recipients use the resolved channels.
        foreach (var recipient in _recipients.Resolve(effective.Recipients, values))
        {
            if (seen.Add(recipient.UserId))
            {
                yield return (recipient, effective.Channels);
            }
        }

        // Subscribers use the channels they subscribed on.
        foreach (var subscription in _subscriptions.Matching(request.Category, request.SourceKey))
        {
            if (!seen.Add(subscription.UserId))
            {
                continue;
            }

            var recipient = ResolveSubscriber(subscription.UserId, values);
            yield return (recipient, subscription.Channels.ToArray());
        }
    }

    private NotificationRecipient ResolveSubscriber(string userId, IReadOnlyDictionary<string, object?> values)
    {
        var resolved = _recipients.Resolve([NotificationAssignment.ToUser(userId)], values);
        return resolved.Count > 0 ? resolved[0] : new NotificationRecipient(userId);
    }

    private static (bool Suppress, NotificationPriority Priority, string? TemplateKey) ApplyRules(
        IReadOnlyList<NotificationRule> rules,
        IReadOnlyDictionary<string, object?> values,
        NotificationPriority priority,
        string? templateKey)
    {
        var suppress = false;
        foreach (var rule in rules)
        {
            if (!rule.Matches(values))
            {
                continue;
            }

            suppress |= rule.Suppress;
            if (rule.Priority is { } rulePriority)
            {
                priority = rulePriority;
            }

            if (rule.TemplateKey is { } ruleTemplate)
            {
                templateKey = ruleTemplate;
            }
        }

        return (suppress, priority, templateKey);
    }

    private Notification Build(
        NotificationRequest request,
        EffectiveSettings effective,
        NotificationContext context,
        NotificationRecipient recipient,
        NotificationChannel channel,
        NotificationPriority priority,
        string? templateKey,
        DateTimeOffset nowUtc)
    {
        var culture = string.IsNullOrWhiteSpace(recipient.Culture) ? context.Culture : recipient.Culture!;
        var (subject, body) = RenderContent(request, context, templateKey, channel, culture);
        var address = recipient.AddressFor(channel)
            ?? (channel is NotificationChannel.InApp or NotificationChannel.SignalR ? recipient.UserId : string.Empty);
        var scheduledFor = ScheduleFor(request, effective.DeliveryPolicy, nowUtc);
        var expiresOn = ExpiryFor(effective, scheduledFor);

        return new Notification(
            context.Tenant,
            request.Category,
            priority,
            channel,
            recipient.UserId,
            address,
            body,
            nowUtc,
            subject,
            effective.DeliveryPolicy,
            effective.Retry,
            scheduledFor,
            culture,
            request.DefinitionKey,
            request.Source,
            request.SourceKey,
            request.SourceId,
            context.CorrelationId,
            expiresOn,
            context.Values,
            request.Attachments);
    }

    private (string? Subject, string Body) RenderContent(
        NotificationRequest request,
        NotificationContext context,
        string? templateKey,
        NotificationChannel channel,
        string culture)
    {
        if (templateKey is not null && _templates.Resolve(templateKey, channel, culture) is { } template)
        {
            var rendered = _templateEngine.Render(template, context.Values);
            return (rendered.Subject, rendered.Body);
        }

        // No template: render the inline subject and body (ad-hoc notifications), substituting tokens too.
        var inlineSubject = request.Subject is null ? null : _templateEngine.Substitute(request.Subject, context.Values);
        var inlineBody = request.Body is null
            ? request.Category.ToString()
            : _templateEngine.Substitute(request.Body, context.Values);
        return (inlineSubject, inlineBody);
    }

    private static DateTimeOffset ScheduleFor(
        NotificationRequest request, NotificationDeliveryPolicy policy, DateTimeOffset nowUtc) => policy switch
        {
            NotificationDeliveryPolicy.Scheduled => request.ScheduledForUtc ?? nowUtc,
            NotificationDeliveryPolicy.Delayed => nowUtc + (request.Delay ?? TimeSpan.Zero),
            _ => nowUtc,
        };

    // A notification expires only when its definition declares a time-to-live, measured from when it is due.
    private static DateTimeOffset? ExpiryFor(EffectiveSettings effective, DateTimeOffset scheduledForUtc) =>
        effective.TimeToLive is { } timeToLive ? scheduledForUtc + timeToLive : null;

    /// <summary>The request's settings after the producing definition's defaults have been merged in.</summary>
    private sealed record EffectiveSettings(
        IReadOnlyList<NotificationAssignment> Recipients,
        IReadOnlyList<NotificationChannel> Channels,
        NotificationPriority Priority,
        NotificationDeliveryPolicy DeliveryPolicy,
        string? TemplateKey,
        NotificationRetryPolicy Retry,
        TimeSpan? TimeToLive);
}
