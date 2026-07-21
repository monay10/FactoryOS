using System.Collections.Concurrent;
using FactoryOS.Plugins.Workflow.Notifications.Domain;

namespace FactoryOS.Plugins.Workflow.Notifications.Execution;

/// <summary>
/// The people directory the notification engine resolves recipients through: it maps roles and groups to their
/// member users, and a user id to a concrete <see cref="NotificationRecipient"/> with per-channel addresses. The
/// engine never talks to an identity provider directly — this is the seam.
/// </summary>
public interface INotificationDirectory
{
    /// <summary>Gets the recipient for a user id, or <see langword="null"/> when the user is unknown.</summary>
    /// <param name="userId">The user id.</param>
    /// <returns>The recipient, or <see langword="null"/>.</returns>
    NotificationRecipient? GetUser(string userId);

    /// <summary>Gets the user ids holding a role.</summary>
    /// <param name="role">The role.</param>
    /// <returns>The user ids.</returns>
    IReadOnlyList<string> UsersInRole(string role);

    /// <summary>Gets the user ids in a group.</summary>
    /// <param name="group">The group.</param>
    /// <returns>The user ids.</returns>
    IReadOnlyList<string> UsersInGroup(string group);
}

/// <summary>An in-memory <see cref="INotificationDirectory"/> populated by registration.</summary>
public sealed class InMemoryNotificationDirectory : INotificationDirectory
{
    private readonly ConcurrentDictionary<string, NotificationRecipient> _users = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, HashSet<string>> _roles = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, HashSet<string>> _groups = new(StringComparer.Ordinal);

    /// <summary>Registers or replaces a user recipient.</summary>
    /// <param name="recipient">The recipient.</param>
    public void AddUser(NotificationRecipient recipient)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        _users[recipient.UserId] = recipient;
    }

    /// <summary>Adds a user to a role.</summary>
    /// <param name="role">The role.</param>
    /// <param name="userId">The user id.</param>
    public void AddToRole(string role, string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var members = _roles.GetOrAdd(role, _ => new HashSet<string>(StringComparer.Ordinal));
        lock (members)
        {
            members.Add(userId);
        }
    }

    /// <summary>Adds a user to a group.</summary>
    /// <param name="group">The group.</param>
    /// <param name="userId">The user id.</param>
    public void AddToGroup(string group, string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(group);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var members = _groups.GetOrAdd(group, _ => new HashSet<string>(StringComparer.Ordinal));
        lock (members)
        {
            members.Add(userId);
        }
    }

    /// <inheritdoc />
    public NotificationRecipient? GetUser(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return _users.TryGetValue(userId, out var recipient) ? recipient : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> UsersInRole(string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        if (!_roles.TryGetValue(role, out var members))
        {
            return [];
        }

        lock (members)
        {
            return members.ToArray();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> UsersInGroup(string group)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(group);
        if (!_groups.TryGetValue(group, out var members))
        {
            return [];
        }

        lock (members)
        {
            return members.ToArray();
        }
    }
}

/// <summary>
/// Resolves a definition's (or a request's) recipient assignments into concrete, de-duplicated recipients.
/// A user assignment yields that user; a role or group expands to its members; a dynamic assignment evaluates
/// its expression over the context values to a user id. Unknown users still yield a recipient carrying just the
/// id, so a missing address surfaces as a delivery failure rather than being silently dropped.
/// </summary>
public sealed class RecipientResolver
{
    private readonly INotificationDirectory _directory;

    /// <summary>Initializes a new instance of the <see cref="RecipientResolver"/> class.</summary>
    /// <param name="directory">The people directory.</param>
    public RecipientResolver(INotificationDirectory directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        _directory = directory;
    }

    /// <summary>Resolves a set of assignments into recipients.</summary>
    /// <param name="assignments">The assignments to resolve.</param>
    /// <param name="values">The context values for dynamic resolution.</param>
    /// <returns>The resolved recipients, de-duplicated by user id.</returns>
    public IReadOnlyList<NotificationRecipient> Resolve(
        IEnumerable<NotificationAssignment> assignments, IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(assignments);
        ArgumentNullException.ThrowIfNull(values);

        var byUser = new Dictionary<string, NotificationRecipient>(StringComparer.Ordinal);
        foreach (var assignment in assignments)
        {
            foreach (var userId in ExpandUserIds(assignment, values))
            {
                if (!string.IsNullOrWhiteSpace(userId) && !byUser.ContainsKey(userId))
                {
                    byUser[userId] = _directory.GetUser(userId) ?? new NotificationRecipient(userId);
                }
            }
        }

        return byUser.Values.ToArray();
    }

    private IEnumerable<string> ExpandUserIds(
        NotificationAssignment assignment, IReadOnlyDictionary<string, object?> values) => assignment.Kind switch
        {
            NotificationRecipientKind.Role => _directory.UsersInRole(assignment.Target),
            NotificationRecipientKind.Group => _directory.UsersInGroup(assignment.Target),
            NotificationRecipientKind.Dynamic => [assignment.ResolveTarget(values)],
            _ => [assignment.Target],
        };
}
