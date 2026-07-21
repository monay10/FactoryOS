using FactoryOS.Plugins.Workflow.Engine.Expressions;

namespace FactoryOS.Plugins.Workflow.Approvals.Domain;

/// <summary>
/// Names who an approval participant is: a specific user, the holders of a role or group, or a principal
/// resolved at runtime from an expression over the approval's context values (workflow variables, form
/// values). Reuses the shared workflow expression language for dynamic resolution.
/// </summary>
public sealed class ApprovalAssignment
{
    private readonly WorkflowExpression? _expression;

    private ApprovalAssignment(ApprovalPrincipalKind kind, string target, bool dynamic)
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
    public ApprovalPrincipalKind Kind { get; }

    /// <summary>Gets the raw target (user id, role, group name or expression).</summary>
    public string Target { get; }

    /// <summary>Gets a value indicating whether the target is a dynamic expression.</summary>
    public bool IsDynamic { get; }

    /// <summary>Creates an assignment to a specific user.</summary>
    /// <param name="user">The user id.</param>
    /// <returns>The assignment.</returns>
    public static ApprovalAssignment User(string user) => Direct(ApprovalPrincipalKind.User, user);

    /// <summary>Creates an assignment to the holders of a role.</summary>
    /// <param name="role">The role.</param>
    /// <returns>The assignment.</returns>
    public static ApprovalAssignment Role(string role) => Direct(ApprovalPrincipalKind.Role, role);

    /// <summary>Creates an assignment to the members of a group.</summary>
    /// <param name="group">The group.</param>
    /// <returns>The assignment.</returns>
    public static ApprovalAssignment Group(string group) => Direct(ApprovalPrincipalKind.Group, group);

    /// <summary>Creates a dynamic assignment resolved from an expression.</summary>
    /// <param name="expression">The expression that yields the assignee.</param>
    /// <returns>The assignment.</returns>
    public static ApprovalAssignment Dynamic(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        return new ApprovalAssignment(ApprovalPrincipalKind.User, expression, dynamic: true);
    }

    /// <summary>Resolves the assignee reference for a set of values.</summary>
    /// <param name="values">The approval context values.</param>
    /// <returns>The resolved assignee reference.</returns>
    public string Resolve(IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (_expression is not null)
        {
            return _expression.Evaluate(values)?.ToString() ?? string.Empty;
        }

        return Kind switch
        {
            ApprovalPrincipalKind.Role => $"role:{Target}",
            ApprovalPrincipalKind.Group => $"group:{Target}",
            _ => Target,
        };
    }

    private static ApprovalAssignment Direct(ApprovalPrincipalKind kind, string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        return new ApprovalAssignment(kind, target, dynamic: false);
    }
}
