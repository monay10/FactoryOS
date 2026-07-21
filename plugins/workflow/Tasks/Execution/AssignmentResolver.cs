using System.Collections.Concurrent;
using FactoryOS.Plugins.Workflow.Tasks.Domain;
using FactoryOS.Plugins.Workflow.Tasks.Persistence;

namespace FactoryOS.Plugins.Workflow.Tasks.Execution;

/// <summary>Expands roles and groups to their member user ids for assignment resolution.</summary>
public interface IHumanTaskDirectory
{
    /// <summary>Lists the members of a role.</summary>
    /// <param name="role">The role.</param>
    /// <returns>The member user ids.</returns>
    IReadOnlyCollection<string> MembersOfRole(string role);

    /// <summary>Lists the members of a group.</summary>
    /// <param name="group">The group.</param>
    /// <returns>The member user ids.</returns>
    IReadOnlyCollection<string> MembersOfGroup(string group);
}

/// <summary>
/// An in-memory <see cref="IHumanTaskDirectory"/>. Empty by default, so role and group assignments resolve to
/// a claimable pool with no members until a real directory is wired in; members can be registered for tests
/// and simple deployments.
/// </summary>
public sealed class InMemoryHumanTaskDirectory : IHumanTaskDirectory
{
    private readonly ConcurrentDictionary<string, HashSet<string>> _roles = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, HashSet<string>> _groups = new(StringComparer.Ordinal);

    /// <summary>Adds a user to a role.</summary>
    /// <param name="role">The role.</param>
    /// <param name="user">The user id.</param>
    public void AddToRole(string role, string user) => Add(_roles, role, user);

    /// <summary>Adds a user to a group.</summary>
    /// <param name="group">The group.</param>
    /// <param name="user">The user id.</param>
    public void AddToGroup(string group, string user) => Add(_groups, group, user);

    /// <inheritdoc />
    public IReadOnlyCollection<string> MembersOfRole(string role) => Members(_roles, role);

    /// <inheritdoc />
    public IReadOnlyCollection<string> MembersOfGroup(string group) => Members(_groups, group);

    private static void Add(ConcurrentDictionary<string, HashSet<string>> map, string key, string user)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(user);
        var set = map.GetOrAdd(key, _ => new HashSet<string>(StringComparer.Ordinal));
        lock (set)
        {
            set.Add(user);
        }
    }

    private static string[] Members(ConcurrentDictionary<string, HashSet<string>> map, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (!map.TryGetValue(key, out var set))
        {
            return [];
        }

        lock (set)
        {
            return set.ToArray();
        }
    }
}

/// <summary>The resolved assignment of a task: the concrete assignee (if one was chosen) and the candidate pool.</summary>
/// <param name="Assignee">The chosen assignee, or <see langword="null"/> for a claimable pool.</param>
/// <param name="Candidates">The candidate pool the assignee was (or may be) chosen from.</param>
public sealed record AssignmentOutcome(string? Assignee, IReadOnlyList<string> Candidates);

/// <summary>
/// Resolves a <see cref="HumanTaskAssignment"/> to a concrete assignee and candidate pool. Direct and dynamic
/// strategies name the assignee; role and group strategies expand to a claimable pool through the directory;
/// round-robin rotates through a fixed pool; load-balanced picks the candidate with the fewest open tasks.
/// </summary>
public sealed class AssignmentResolver
{
    private readonly IHumanTaskDirectory _directory;
    private readonly IHumanTaskStore _store;
    private readonly ConcurrentDictionary<string, int> _roundRobinCursors = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="AssignmentResolver"/> class.</summary>
    /// <param name="directory">The directory used to expand roles and groups.</param>
    /// <param name="store">The task store used to measure load for load-balancing.</param>
    public AssignmentResolver(IHumanTaskDirectory directory, IHumanTaskStore store)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(store);
        _directory = directory;
        _store = store;
    }

    /// <summary>Resolves an assignment against a set of values.</summary>
    /// <param name="assignment">The assignment to resolve.</param>
    /// <param name="values">The workflow and form values (for dynamic assignments).</param>
    /// <returns>The resolved outcome.</returns>
    public AssignmentOutcome Resolve(
        HumanTaskAssignment assignment, IReadOnlyDictionary<string, object?>? values = null)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        var context = values ?? EmptyValues;

        return assignment.Strategy switch
        {
            AssignmentStrategy.User => new AssignmentOutcome(assignment.Target, [assignment.Target!]),
            AssignmentStrategy.Dynamic => Direct(assignment.ResolveDynamic(context)),
            AssignmentStrategy.Role => Claimable(_directory.MembersOfRole(assignment.Target!)),
            AssignmentStrategy.Group => Claimable(_directory.MembersOfGroup(assignment.Target!)),
            AssignmentStrategy.RoundRobin => RoundRobin(assignment.Candidates),
            AssignmentStrategy.LoadBalanced => LoadBalanced(assignment.Candidates),
            _ => new AssignmentOutcome(null, []),
        };
    }

    private static AssignmentOutcome Direct(string? assignee) =>
        new(assignee, assignee is null ? [] : [assignee]);

    private static AssignmentOutcome Claimable(IReadOnlyCollection<string> members) =>
        new(null, members.ToArray());

    private AssignmentOutcome RoundRobin(IReadOnlyList<string> candidates)
    {
        var key = string.Join('|', candidates);
        var index = _roundRobinCursors.AddOrUpdate(key, 0, (_, current) => current + 1);
        var chosen = candidates[index % candidates.Count];
        return new AssignmentOutcome(chosen, candidates);
    }

    private AssignmentOutcome LoadBalanced(IReadOnlyList<string> candidates)
    {
        var chosen = candidates
            .OrderBy(candidate => _store.ListByAssignee(candidate).Count(task => !task.IsFinished))
            .ThenBy(candidate => candidate, StringComparer.Ordinal)
            .First();
        return new AssignmentOutcome(chosen, candidates);
    }

    private static readonly IReadOnlyDictionary<string, object?> EmptyValues =
        new Dictionary<string, object?>(StringComparer.Ordinal);
}
