using System.Collections.Concurrent;
using FactoryOS.Plugins.Workflow.Notifications.Domain;

namespace FactoryOS.Plugins.Workflow.Notifications.Execution;

/// <summary>Holds recipients' delivery preferences.</summary>
public interface INotificationPreferenceStore
{
    /// <summary>Registers or replaces a recipient's preference.</summary>
    /// <param name="preference">The preference.</param>
    void Set(NotificationPreference preference);

    /// <summary>Gets a recipient's preference, or <see langword="null"/> when none is set.</summary>
    /// <param name="userId">The user id.</param>
    /// <returns>The preference, or <see langword="null"/>.</returns>
    NotificationPreference? Get(string userId);
}

/// <summary>An in-memory <see cref="INotificationPreferenceStore"/>.</summary>
public sealed class InMemoryNotificationPreferenceStore : INotificationPreferenceStore
{
    private readonly ConcurrentDictionary<string, NotificationPreference> _preferences = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void Set(NotificationPreference preference)
    {
        ArgumentNullException.ThrowIfNull(preference);
        _preferences[preference.UserId] = preference;
    }

    /// <inheritdoc />
    public NotificationPreference? Get(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return _preferences.TryGetValue(userId, out var preference) ? preference : null;
    }
}

/// <summary>Holds standing subscriptions and finds the ones matching a category and source key.</summary>
public interface INotificationSubscriptionStore
{
    /// <summary>Adds a subscription.</summary>
    /// <param name="subscription">The subscription.</param>
    void Add(NotificationSubscription subscription);

    /// <summary>Gets the subscriptions that match a category and source key.</summary>
    /// <param name="category">The category.</param>
    /// <param name="sourceKey">The source key, if any.</param>
    /// <returns>The matching subscriptions.</returns>
    IReadOnlyList<NotificationSubscription> Matching(NotificationCategory category, string? sourceKey);
}

/// <summary>An in-memory <see cref="INotificationSubscriptionStore"/>.</summary>
public sealed class InMemoryNotificationSubscriptionStore : INotificationSubscriptionStore
{
    private readonly List<NotificationSubscription> _subscriptions = [];
    private readonly Lock _gate = new();

    /// <inheritdoc />
    public void Add(NotificationSubscription subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        lock (_gate)
        {
            _subscriptions.Add(subscription);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<NotificationSubscription> Matching(NotificationCategory category, string? sourceKey)
    {
        lock (_gate)
        {
            return _subscriptions.Where(subscription => subscription.Matches(category, sourceKey)).ToArray();
        }
    }
}

/// <summary>
/// Applies a recipient's preferences to a set of candidate channels: it drops channels the recipient does not
/// accept, drops every channel when the category is muted, and — outside of <see cref="NotificationPriority.Critical"/>
/// notifications — drops channels during the recipient's quiet hours. The result is the channels a notification
/// should actually go out on; an empty result means the notification is suppressed for that recipient.
/// </summary>
public sealed class PreferenceResolver
{
    private readonly INotificationPreferenceStore _preferences;

    /// <summary>Initializes a new instance of the <see cref="PreferenceResolver"/> class.</summary>
    /// <param name="preferences">The preference store.</param>
    public PreferenceResolver(INotificationPreferenceStore preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        _preferences = preferences;
    }

    /// <summary>Resolves the channels a notification should be delivered on for a recipient.</summary>
    /// <param name="userId">The recipient user id.</param>
    /// <param name="category">The notification category.</param>
    /// <param name="priority">The notification priority.</param>
    /// <param name="candidateChannels">The channels the notification would otherwise use.</param>
    /// <param name="nowUtc">The current time (its time-of-day is compared to quiet hours).</param>
    /// <returns>The channels to deliver on; empty when the notification is suppressed.</returns>
    public IReadOnlyList<NotificationChannel> ResolveChannels(
        string userId,
        NotificationCategory category,
        NotificationPriority priority,
        IReadOnlyCollection<NotificationChannel> candidateChannels,
        DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(candidateChannels);

        var preference = _preferences.Get(userId);
        if (preference is null)
        {
            return candidateChannels.Distinct().ToArray();
        }

        // Critical notifications override mute and quiet hours but still respect the channel allow-list.
        var critical = priority == NotificationPriority.Critical;
        if (!critical && preference.IsMuted(category))
        {
            return [];
        }

        if (!critical && preference.IsQuietAt(TimeOnly.FromDateTime(nowUtc.UtcDateTime)))
        {
            return [];
        }

        return candidateChannels.Distinct().Where(preference.AllowsChannel).ToArray();
    }
}
