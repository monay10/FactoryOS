using FactoryOS.Plugins.Workflow.Engine.Expressions;

namespace FactoryOS.Plugins.Forms.Engine.Domain;

/// <summary>
/// Grants a principal a level of access to a form. The <see cref="Principal"/> is interpreted according to
/// <see cref="Kind"/> (a user id, a role, a group, or an expression resolved at runtime).
/// </summary>
/// <param name="Kind">How to interpret the principal.</param>
/// <param name="Principal">The user id, role, group name or expression.</param>
/// <param name="Access">The access the grant confers.</param>
public sealed record FormPermission(FormPrincipalKind Kind, string Principal, FormAccess Access);

/// <summary>
/// Assigns responsibility for filling a form to a principal. A dynamic assignment resolves its target from an
/// expression over the form (or workflow) values at open time.
/// </summary>
public sealed class FormAssignment
{
    private readonly WorkflowExpression? _expression;

    /// <summary>Initializes a new instance of the <see cref="FormAssignment"/> class.</summary>
    /// <param name="kind">How to interpret the target.</param>
    /// <param name="target">The user id, role, group name or expression.</param>
    public FormAssignment(FormPrincipalKind kind, string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        if (kind == FormPrincipalKind.Dynamic)
        {
            _expression = WorkflowExpression.Parse(target);
        }

        Kind = kind;
        Target = target;
    }

    /// <summary>Gets how to interpret the target.</summary>
    public FormPrincipalKind Kind { get; }

    /// <summary>Gets the raw target (user id, role, group or expression).</summary>
    public string Target { get; }

    /// <summary>Creates an assignment to a specific user.</summary>
    /// <param name="user">The user id.</param>
    /// <returns>The assignment.</returns>
    public static FormAssignment User(string user) => new(FormPrincipalKind.User, user);

    /// <summary>Creates an assignment to a role.</summary>
    /// <param name="role">The role.</param>
    /// <returns>The assignment.</returns>
    public static FormAssignment Role(string role) => new(FormPrincipalKind.Role, role);

    /// <summary>Creates an assignment to a group.</summary>
    /// <param name="group">The group.</param>
    /// <returns>The assignment.</returns>
    public static FormAssignment Group(string group) => new(FormPrincipalKind.Group, group);

    /// <summary>Creates a dynamic assignment resolved from an expression.</summary>
    /// <param name="expression">The expression that yields the assignee.</param>
    /// <returns>The assignment.</returns>
    public static FormAssignment Dynamic(string expression) => new(FormPrincipalKind.Dynamic, expression);

    /// <summary>Resolves the concrete assignee for a set of values.</summary>
    /// <param name="values">The form values.</param>
    /// <returns>The resolved assignee, or <see langword="null"/> when a dynamic expression yields nothing.</returns>
    public string? Resolve(IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (_expression is null)
        {
            return Target;
        }

        return _expression.Evaluate(values)?.ToString();
    }
}
