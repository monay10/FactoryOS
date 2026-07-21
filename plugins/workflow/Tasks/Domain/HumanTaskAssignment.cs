using FactoryOS.Plugins.Workflow.Engine.Expressions;

namespace FactoryOS.Plugins.Workflow.Tasks.Domain;

/// <summary>
/// Describes how a human task's owner is chosen. Direct strategies (<see cref="AssignmentStrategy.User"/>)
/// name the assignee; candidate strategies (role, group, round-robin, load-balanced) name a pool the resolver
/// picks from; a dynamic assignment evaluates an expression over the workflow and form values. The resolution
/// itself lives in the execution layer's <c>AssignmentResolver</c>; this type only carries the intent.
/// </summary>
public sealed class HumanTaskAssignment
{
    private readonly WorkflowExpression? _expression;

    private HumanTaskAssignment(
        AssignmentStrategy strategy, string? target, IReadOnlyList<string> candidates)
    {
        Strategy = strategy;
        Target = target;
        Candidates = candidates;
        if (strategy == AssignmentStrategy.Dynamic)
        {
            _expression = WorkflowExpression.Parse(target!);
        }
    }

    /// <summary>Gets the assignment strategy.</summary>
    public AssignmentStrategy Strategy { get; }

    /// <summary>Gets the direct target (user id, role, group name or expression), when the strategy has one.</summary>
    public string? Target { get; }

    /// <summary>Gets the candidate pool for round-robin and load-balanced strategies.</summary>
    public IReadOnlyList<string> Candidates { get; }

    /// <summary>Creates an assignment to a specific user.</summary>
    /// <param name="user">The user id.</param>
    /// <returns>The assignment.</returns>
    public static HumanTaskAssignment ToUser(string user)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(user);
        return new HumanTaskAssignment(AssignmentStrategy.User, user, []);
    }

    /// <summary>Creates an assignment to the holders of a role.</summary>
    /// <param name="role">The role.</param>
    /// <returns>The assignment.</returns>
    public static HumanTaskAssignment ToRole(string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        return new HumanTaskAssignment(AssignmentStrategy.Role, role, []);
    }

    /// <summary>Creates an assignment to the members of a group.</summary>
    /// <param name="group">The group.</param>
    /// <returns>The assignment.</returns>
    public static HumanTaskAssignment ToGroup(string group)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(group);
        return new HumanTaskAssignment(AssignmentStrategy.Group, group, []);
    }

    /// <summary>Creates a dynamic assignment resolved from an expression.</summary>
    /// <param name="expression">The expression that yields the assignee.</param>
    /// <returns>The assignment.</returns>
    public static HumanTaskAssignment ToExpression(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        return new HumanTaskAssignment(AssignmentStrategy.Dynamic, expression, []);
    }

    /// <summary>Creates a round-robin assignment over a candidate pool.</summary>
    /// <param name="candidates">The candidate pool.</param>
    /// <returns>The assignment.</returns>
    public static HumanTaskAssignment RoundRobin(params string[] candidates) =>
        FromPool(AssignmentStrategy.RoundRobin, candidates);

    /// <summary>Creates a load-balanced assignment over a candidate pool.</summary>
    /// <param name="candidates">The candidate pool.</param>
    /// <returns>The assignment.</returns>
    public static HumanTaskAssignment LoadBalanced(params string[] candidates) =>
        FromPool(AssignmentStrategy.LoadBalanced, candidates);

    /// <summary>Evaluates a dynamic assignment against a set of values.</summary>
    /// <param name="values">The workflow and form values.</param>
    /// <returns>The resolved assignee, or <see langword="null"/>.</returns>
    public string? ResolveDynamic(IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return _expression?.Evaluate(values)?.ToString();
    }

    private static HumanTaskAssignment FromPool(AssignmentStrategy strategy, string[] candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Length == 0)
        {
            throw new ArgumentException("A candidate pool must contain at least one candidate.", nameof(candidates));
        }

        return new HumanTaskAssignment(strategy, null, candidates.ToArray());
    }
}
