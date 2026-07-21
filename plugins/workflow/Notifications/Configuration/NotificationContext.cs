using FactoryOS.Plugins.Workflow.Notifications.Domain;

namespace FactoryOS.Plugins.Workflow.Notifications.Configuration;

/// <summary>Stable constants for the notification engine.</summary>
public static class NotificationConstants
{
    /// <summary>The configuration section the engine options bind from.</summary>
    public const string ConfigurationSection = "Workflow:Notifications";

    /// <summary>The tenant used when a caller supplies none.</summary>
    public const string DefaultTenant = "default";

    /// <summary>The culture used when a notification declares no localization.</summary>
    public const string DefaultCulture = "en";
}

/// <summary>
/// Runtime options for the notification engine (namespace <c>Notifications.Configuration</c>). These govern the
/// notification runtime only; they are independent of the workflow, forms, human task and approval engines'
/// own options. The notification engine subscribes to those engines' events and never modifies them.
/// </summary>
public sealed record NotificationEngineOptions
{
    /// <summary>Gets the default number of delivery attempts before a notification is dead-lettered.</summary>
    public int DefaultMaxAttempts { get; init; } = 3;

    /// <summary>Gets the base back-off applied between retries; each attempt multiplies it by the attempt number.</summary>
    public TimeSpan RetryBackoff { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>Gets the maximum number of notifications a single queue-processing pass delivers.</summary>
    public int DueWorkBatchSize { get; init; } = 200;

    /// <summary>
    /// Gets a value indicating whether the engine automatically produces notifications for the workflow, human
    /// task, approval and forms events it subscribes to. When <see langword="false"/> the subscribers are inert
    /// and notifications are only produced by explicit <c>NotifyAsync</c> calls.
    /// </summary>
    public bool SubscribeToEngineEvents { get; init; } = true;

    /// <summary>Gets the default culture used when a notification declares no localization.</summary>
    public string DefaultCulture { get; init; } = NotificationConstants.DefaultCulture;
}

/// <summary>
/// The caller-supplied context a notification is produced within: the owning tenant, an optional initiator, the
/// correlation of the source event, and the values (workflow variables, task/approval/form values) that render
/// templates and resolve dynamic recipients and rules. The tenant is stamped onto every notification so nothing
/// crosses tenants.
/// </summary>
public sealed record NotificationContext
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyValues =
        new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="NotificationContext"/> record.</summary>
    /// <param name="tenant">The owning tenant.</param>
    /// <param name="culture">The culture used to localize and render text.</param>
    /// <param name="initiatedBy">Who initiated the source action, if known.</param>
    /// <param name="correlationId">The correlation id carried from the source event, if any.</param>
    /// <param name="values">The context values for template rendering and dynamic resolution.</param>
    public NotificationContext(
        string tenant,
        string? culture = null,
        string? initiatedBy = null,
        string? correlationId = null,
        IReadOnlyDictionary<string, object?>? values = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        Tenant = tenant;
        Culture = string.IsNullOrWhiteSpace(culture) ? NotificationConstants.DefaultCulture : culture;
        InitiatedBy = initiatedBy;
        CorrelationId = correlationId;
        Values = values ?? EmptyValues;
    }

    /// <summary>Gets the owning tenant.</summary>
    public string Tenant { get; }

    /// <summary>Gets the culture used to localize and render text.</summary>
    public string Culture { get; }

    /// <summary>Gets who initiated the source action, if known.</summary>
    public string? InitiatedBy { get; }

    /// <summary>Gets the correlation id carried from the source event, if any.</summary>
    public string? CorrelationId { get; }

    /// <summary>Gets the context values used to render templates and resolve dynamic recipients and rules.</summary>
    public IReadOnlyDictionary<string, object?> Values { get; }

    /// <summary>A context for the default tenant with no values.</summary>
    public static NotificationContext Default { get; } = new(NotificationConstants.DefaultTenant);
}
