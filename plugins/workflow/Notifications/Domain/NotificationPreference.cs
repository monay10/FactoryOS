namespace FactoryOS.Plugins.Workflow.Notifications.Domain;

/// <summary>
/// A recipient's delivery preferences: which channels they will accept, which categories they have muted, and
/// an optional quiet-hours window during which only <see cref="NotificationPriority.Critical"/> notifications
/// are delivered (others are held or suppressed). A missing preference means "deliver on every channel".
/// </summary>
public sealed class NotificationPreference
{
    private readonly HashSet<NotificationChannel> _allowedChannels;
    private readonly HashSet<NotificationCategory> _mutedCategories;

    /// <summary>Initializes a new instance of the <see cref="NotificationPreference"/> class.</summary>
    /// <param name="userId">The user the preference belongs to.</param>
    /// <param name="allowedChannels">The channels the user accepts, or <see langword="null"/> for all channels.</param>
    /// <param name="mutedCategories">The categories the user has muted.</param>
    /// <param name="quietHoursStart">The start of the daily quiet-hours window, if any.</param>
    /// <param name="quietHoursEnd">The end of the daily quiet-hours window, if any.</param>
    public NotificationPreference(
        string userId,
        IReadOnlyCollection<NotificationChannel>? allowedChannels = null,
        IReadOnlyCollection<NotificationCategory>? mutedCategories = null,
        TimeOnly? quietHoursStart = null,
        TimeOnly? quietHoursEnd = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        UserId = userId;
        _allowedChannels = allowedChannels is null ? [] : [.. allowedChannels];
        _mutedCategories = mutedCategories is null ? [] : [.. mutedCategories];
        QuietHoursStart = quietHoursStart;
        QuietHoursEnd = quietHoursEnd;
    }

    /// <summary>Gets the user the preference belongs to.</summary>
    public string UserId { get; }

    /// <summary>Gets the start of the daily quiet-hours window, if any.</summary>
    public TimeOnly? QuietHoursStart { get; }

    /// <summary>Gets the end of the daily quiet-hours window, if any.</summary>
    public TimeOnly? QuietHoursEnd { get; }

    /// <summary>Gets a value indicating whether a channel is accepted (an empty allow-list accepts all).</summary>
    /// <param name="channel">The channel.</param>
    /// <returns><see langword="true"/> when the channel is accepted.</returns>
    public bool AllowsChannel(NotificationChannel channel) =>
        _allowedChannels.Count == 0 || _allowedChannels.Contains(channel);

    /// <summary>Gets a value indicating whether a category is muted.</summary>
    /// <param name="category">The category.</param>
    /// <returns><see langword="true"/> when the category is muted.</returns>
    public bool IsMuted(NotificationCategory category) => _mutedCategories.Contains(category);

    /// <summary>Gets a value indicating whether the given local time falls inside the quiet-hours window.</summary>
    /// <param name="localTime">The recipient's local time.</param>
    /// <returns><see langword="true"/> when inside quiet hours.</returns>
    public bool IsQuietAt(TimeOnly localTime)
    {
        if (QuietHoursStart is not { } start || QuietHoursEnd is not { } end)
        {
            return false;
        }

        // A window that wraps past midnight (e.g. 22:00–07:00) is inside when the time is after the start OR
        // before the end; a same-day window is inside when the time is between the two.
        return start <= end
            ? localTime >= start && localTime < end
            : localTime >= start || localTime < end;
    }
}
