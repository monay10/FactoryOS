using FactoryOS.Plugins.Workflow.Engine.Domain;
using FactoryOS.Plugins.Workflow.Engine.Expressions;

namespace FactoryOS.Plugins.Workflow.Engine.Assignments;

/// <summary>
/// Determines who an activity is assigned to. Resolution runs against the instance's variables so a
/// dynamic assignment can pick an assignee from process data. The resolved value is an opaque assignee
/// reference (e.g. a user id, <c>role:{name}</c> or <c>group:{name}</c>) — this runtime does not resolve
/// identities, it only records the assignment.
/// </summary>
public abstract record WorkflowAssignment
{
    /// <summary>Resolves the assignee reference for the given variables.</summary>
    /// <param name="variables">The instance variables.</param>
    /// <returns>The assignee reference.</returns>
    public abstract string Resolve(WorkflowVariables variables);
}

/// <summary>Assigns an activity to a specific user.</summary>
/// <param name="UserId">The user identifier.</param>
public sealed record UserAssignment(string UserId) : WorkflowAssignment
{
    /// <inheritdoc />
    public override string Resolve(WorkflowVariables variables) => UserId;
}

/// <summary>Assigns an activity to every member of a role.</summary>
/// <param name="Role">The role name.</param>
public sealed record RoleAssignment(string Role) : WorkflowAssignment
{
    /// <inheritdoc />
    public override string Resolve(WorkflowVariables variables) => $"role:{Role}";
}

/// <summary>Assigns an activity to every member of a group.</summary>
/// <param name="Group">The group name.</param>
public sealed record GroupAssignment(string Group) : WorkflowAssignment
{
    /// <inheritdoc />
    public override string Resolve(WorkflowVariables variables) => $"group:{Group}";
}

/// <summary>
/// Assigns an activity to an assignee computed from process data by evaluating an expression against the
/// instance variables (for example <c>'user:' + approver</c>).
/// </summary>
public sealed record DynamicAssignment : WorkflowAssignment
{
    private readonly WorkflowExpression _expression;

    /// <summary>Initializes a new instance of the <see cref="DynamicAssignment"/> record.</summary>
    /// <param name="expression">The assignee expression.</param>
    public DynamicAssignment(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        _expression = WorkflowExpression.Parse(expression);
    }

    /// <summary>Gets the assignee expression text.</summary>
    public string Expression => _expression.ToString();

    /// <inheritdoc />
    public override string Resolve(WorkflowVariables variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        return _expression.Evaluate(variables.AsReadOnly())?.ToString() ?? string.Empty;
    }
}
