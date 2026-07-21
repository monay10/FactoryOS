using FactoryOS.Plugins.Workflow.Engine.Domain;
using FactoryOS.Plugins.Workflow.Engine.Expressions;

namespace FactoryOS.Plugins.Workflow.Engine.Transitions;

/// <summary>
/// A directed edge between two nodes, optionally guarded by a condition. An unconditional transition
/// (no condition) always applies — used as the default (<c>else</c>) branch of a decision.
/// </summary>
public sealed record WorkflowTransition
{
    private readonly WorkflowExpression? _condition;

    /// <summary>Initializes a new instance of the <see cref="WorkflowTransition"/> record.</summary>
    /// <param name="from">The source node id.</param>
    /// <param name="to">The target node id.</param>
    /// <param name="condition">The optional guard expression; <see langword="null"/> for an unconditional edge.</param>
    public WorkflowTransition(string from, string to, string? condition = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(from);
        ArgumentException.ThrowIfNullOrWhiteSpace(to);

        From = from;
        To = to;
        _condition = string.IsNullOrWhiteSpace(condition) ? null : WorkflowExpression.Parse(condition);
    }

    /// <summary>Gets the source node id.</summary>
    public string From { get; }

    /// <summary>Gets the target node id.</summary>
    public string To { get; }

    /// <summary>Gets a value indicating whether the transition is guarded by a condition.</summary>
    public bool IsConditional => _condition is not null;

    /// <summary>Gets the guard expression text, or <see langword="null"/> when unconditional.</summary>
    public string? Condition => _condition?.ToString();

    /// <summary>Evaluates the transition guard against the variables.</summary>
    /// <param name="variables">The instance variables.</param>
    /// <returns><see langword="true"/> when unconditional or the guard holds.</returns>
    public bool IsSatisfiedBy(WorkflowVariables variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        return _condition is null || _condition.EvaluateBoolean(variables.AsReadOnly());
    }
}
