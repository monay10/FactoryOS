using FactoryOS.Plugins.Workflow.Engine.Expressions;

namespace FactoryOS.Plugins.Workflow.Notifications.Domain;

/// <summary>
/// Names who should receive a notification: a specific user, the holders of a role or group, or a principal
/// resolved at runtime from an expression over the notification's context values. Reuses the shared workflow
/// expression language for dynamic resolution.
/// </summary>
public sealed class NotificationAssignment
{
    private readonly WorkflowExpression? _expression;

    private NotificationAssignment(NotificationRecipientKind kind, string target, bool dynamic)
    {
        Kind = kind;
        Target = target;
        IsDynamic = dynamic;
        if (dynamic)
        {
            _expression = WorkflowExpression.Parse(target);
        }
    }

    /// <summary>Gets how to interpret the target.</summary>
    public NotificationRecipientKind Kind { get; }

    /// <summary>Gets the raw target (user id, role, group name or expression).</summary>
    public string Target { get; }

    /// <summary>Gets a value indicating whether the target is a dynamic expression.</summary>
    public bool IsDynamic { get; }

    /// <summary>Creates an assignment to a specific user.</summary>
    /// <param name="user">The user id.</param>
    /// <returns>The assignment.</returns>
    public static NotificationAssignment ToUser(string user) => Direct(NotificationRecipientKind.User, user);

    /// <summary>Creates an assignment to the holders of a role.</summary>
    /// <param name="role">The role.</param>
    /// <returns>The assignment.</returns>
    public static NotificationAssignment ToRole(string role) => Direct(NotificationRecipientKind.Role, role);

    /// <summary>Creates an assignment to the members of a group.</summary>
    /// <param name="group">The group.</param>
    /// <returns>The assignment.</returns>
    public static NotificationAssignment ToGroup(string group) => Direct(NotificationRecipientKind.Group, group);

    /// <summary>Creates a dynamic assignment whose target user id is resolved from an expression.</summary>
    /// <param name="expression">The expression that yields the target user id.</param>
    /// <returns>The assignment.</returns>
    public static NotificationAssignment Dynamic(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        return new NotificationAssignment(NotificationRecipientKind.Dynamic, expression, dynamic: true);
    }

    /// <summary>Resolves the dynamic target user id for a set of values (dynamic assignments only).</summary>
    /// <param name="values">The notification context values.</param>
    /// <returns>The resolved target, or the raw target for non-dynamic assignments.</returns>
    public string ResolveTarget(IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return _expression is not null ? _expression.Evaluate(values)?.ToString() ?? string.Empty : Target;
    }

    private static NotificationAssignment Direct(NotificationRecipientKind kind, string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        return new NotificationAssignment(kind, target, dynamic: false);
    }
}

/// <summary>
/// A concrete, resolved recipient of a notification: a single user with the addresses to reach them on each
/// channel (e-mail, phone, device token, webhook URL, chat handle, in-app id), a display name and a preferred
/// culture. Produced by the recipient resolver from an assignment.
/// </summary>
public sealed class NotificationRecipient
{
    private readonly Dictionary<NotificationChannel, string> _addresses;

    /// <summary>Initializes a new instance of the <see cref="NotificationRecipient"/> class.</summary>
    /// <param name="userId">The user id.</param>
    /// <param name="displayName">The display name, or <see langword="null"/> to use the id.</param>
    /// <param name="culture">The recipient's preferred culture, or <see langword="null"/> for the default.</param>
    /// <param name="addresses">The per-channel addresses to reach the recipient on.</param>
    public NotificationRecipient(
        string userId,
        string? displayName = null,
        string? culture = null,
        IReadOnlyDictionary<NotificationChannel, string>? addresses = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        UserId = userId;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? userId : displayName;
        Culture = culture;
        _addresses = addresses is null
            ? new Dictionary<NotificationChannel, string>()
            : new Dictionary<NotificationChannel, string>(addresses);
    }

    /// <summary>Gets the user id.</summary>
    public string UserId { get; }

    /// <summary>Gets the display name.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the recipient's preferred culture, if any.</summary>
    public string? Culture { get; }

    /// <summary>Gets the per-channel addresses.</summary>
    public IReadOnlyDictionary<NotificationChannel, string> Addresses => _addresses;

    /// <summary>Sets the address to reach the recipient on a channel.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="address">The address.</param>
    /// <returns>The same recipient, to allow chaining.</returns>
    public NotificationRecipient WithAddress(NotificationChannel channel, string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        _addresses[channel] = address;
        return this;
    }

    /// <summary>Gets the address for a channel, or <see langword="null"/> when the recipient has none.</summary>
    /// <param name="channel">The channel.</param>
    /// <returns>The address, or <see langword="null"/>.</returns>
    public string? AddressFor(NotificationChannel channel) =>
        _addresses.TryGetValue(channel, out var address) ? address : null;
}
