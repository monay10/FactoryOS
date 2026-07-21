using System.Collections.Concurrent;
using FactoryOS.Plugins.Workflow.Notifications.Domain;

namespace FactoryOS.Plugins.Workflow.Notifications.Persistence;

/// <summary>The registry of notification definitions, keyed by definition key.</summary>
public interface INotificationRepository
{
    /// <summary>Registers a definition (idempotent by key; last registration wins).</summary>
    /// <param name="definition">The definition to register.</param>
    void Register(NotificationDefinition definition);

    /// <summary>Gets a definition by key.</summary>
    /// <param name="key">The definition key.</param>
    /// <returns>The definition, or <see langword="null"/> when not registered.</returns>
    NotificationDefinition? Get(string key);

    /// <summary>Gets every registered definition.</summary>
    /// <returns>The definitions.</returns>
    IReadOnlyCollection<NotificationDefinition> All();
}

/// <summary>An in-memory <see cref="INotificationRepository"/>.</summary>
public sealed class InMemoryNotificationRepository : INotificationRepository
{
    private readonly ConcurrentDictionary<string, NotificationDefinition> _definitions = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void Register(NotificationDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _definitions[definition.Key] = definition;
    }

    /// <inheritdoc />
    public NotificationDefinition? Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _definitions.TryGetValue(key, out var definition) ? definition : null;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<NotificationDefinition> All() => _definitions.Values.ToArray();
}

/// <summary>The persistence store for notification instances.</summary>
public interface INotificationStore
{
    /// <summary>Saves a notification (insert or update by id).</summary>
    /// <param name="notification">The notification to save.</param>
    void Save(Notification notification);

    /// <summary>Gets a notification by id.</summary>
    /// <param name="id">The notification id.</param>
    /// <returns>The notification, or <see langword="null"/> when not found.</returns>
    Notification? Get(Guid id);

    /// <summary>Lists the notifications that are pending and due at or before the given time, best-first.</summary>
    /// <param name="nowUtc">The current time.</param>
    /// <param name="max">The maximum number to return.</param>
    /// <returns>The due notifications, ordered by priority then schedule.</returns>
    IReadOnlyList<Notification> ListDue(DateTimeOffset nowUtc, int max);

    /// <summary>Lists the notifications in a given status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The matching notifications.</returns>
    IReadOnlyCollection<Notification> ListByStatus(NotificationStatus status);

    /// <summary>Lists the notifications addressed to a recipient.</summary>
    /// <param name="userId">The recipient user id.</param>
    /// <returns>The recipient's notifications.</returns>
    IReadOnlyCollection<Notification> ListByRecipient(string userId);

    /// <summary>Lists every notification still waiting in the queue.</summary>
    /// <returns>The pending notifications.</returns>
    IReadOnlyCollection<Notification> ListPending();
}

/// <summary>An in-memory <see cref="INotificationStore"/>. Notifications are held by reference, so saves are updates.</summary>
public sealed class InMemoryNotificationStore : INotificationStore
{
    private readonly ConcurrentDictionary<Guid, Notification> _notifications = new();

    /// <inheritdoc />
    public void Save(Notification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        _notifications[notification.Id] = notification;
    }

    /// <inheritdoc />
    public Notification? Get(Guid id) => _notifications.TryGetValue(id, out var notification) ? notification : null;

    /// <inheritdoc />
    public IReadOnlyList<Notification> ListDue(DateTimeOffset nowUtc, int max)
    {
        if (max <= 0)
        {
            return [];
        }

        return _notifications.Values
            .Where(notification => notification.IsDue(nowUtc))
            .OrderByDescending(notification => notification.Priority)
            .ThenBy(notification => notification.ScheduledForUtc)
            .Take(max)
            .ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Notification> ListByStatus(NotificationStatus status) =>
        _notifications.Values.Where(notification => notification.Status == status).ToArray();

    /// <inheritdoc />
    public IReadOnlyCollection<Notification> ListByRecipient(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return _notifications.Values
            .Where(notification => string.Equals(notification.RecipientUserId, userId, StringComparison.Ordinal))
            .ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Notification> ListPending() =>
        _notifications.Values.Where(notification => notification.IsPending).ToArray();
}

/// <summary>The persistence store for notification history entries, queryable independently of the notification.</summary>
public interface INotificationHistoryRepository
{
    /// <summary>Appends a history entry.</summary>
    /// <param name="entry">The entry.</param>
    void Append(NotificationHistoryEntry entry);

    /// <summary>Lists the history entries for a notification, oldest first.</summary>
    /// <param name="notificationId">The notification id.</param>
    /// <returns>The entries.</returns>
    IReadOnlyList<NotificationHistoryEntry> ByNotification(Guid notificationId);
}

/// <summary>An in-memory <see cref="INotificationHistoryRepository"/>.</summary>
public sealed class InMemoryNotificationHistoryRepository : INotificationHistoryRepository
{
    private readonly ConcurrentDictionary<Guid, List<NotificationHistoryEntry>> _byNotification = new();

    /// <inheritdoc />
    public void Append(NotificationHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var list = _byNotification.GetOrAdd(entry.NotificationId, _ => []);
        lock (list)
        {
            list.Add(entry);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<NotificationHistoryEntry> ByNotification(Guid notificationId)
    {
        if (!_byNotification.TryGetValue(notificationId, out var list))
        {
            return [];
        }

        lock (list)
        {
            return list.OrderBy(entry => entry.OccurredOnUtc).ToArray();
        }
    }
}

/// <summary>The registry of notification templates, keyed by template key, channel and culture.</summary>
public interface INotificationTemplateRepository
{
    /// <summary>Registers a template (idempotent by key + channel + culture; last registration wins).</summary>
    /// <param name="template">The template to register.</param>
    void Register(NotificationTemplate template);

    /// <summary>
    /// Gets the best template for a key on a channel in a culture, falling back to the default culture and then
    /// to any culture registered for the key and channel.
    /// </summary>
    /// <param name="key">The template key.</param>
    /// <param name="channel">The channel.</param>
    /// <param name="culture">The requested culture.</param>
    /// <returns>The template, or <see langword="null"/> when none is registered for the key and channel.</returns>
    NotificationTemplate? Resolve(string key, NotificationChannel channel, string culture);

    /// <summary>Gets every registered template.</summary>
    /// <returns>The templates.</returns>
    IReadOnlyCollection<NotificationTemplate> All();
}

/// <summary>An in-memory <see cref="INotificationTemplateRepository"/>.</summary>
public sealed class InMemoryNotificationTemplateRepository : INotificationTemplateRepository
{
    private readonly ConcurrentDictionary<string, NotificationTemplate> _templates = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void Register(NotificationTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        _templates[template.ToRegistryKey()] = template;
    }

    /// <inheritdoc />
    public NotificationTemplate? Resolve(string key, NotificationChannel channel, string culture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);

        if (_templates.TryGetValue(NotificationTemplate.RegistryKey(key, channel, culture), out var exact))
        {
            return exact;
        }

        if (_templates.TryGetValue(
            NotificationTemplate.RegistryKey(key, channel, Configuration.NotificationConstants.DefaultCulture),
            out var defaultCulture))
        {
            return defaultCulture;
        }

        return _templates.Values.FirstOrDefault(
            template => string.Equals(template.Key, key, StringComparison.Ordinal) && template.Channel == channel);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<NotificationTemplate> All() => _templates.Values.ToArray();
}
