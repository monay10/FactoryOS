using FactoryOS.Plugins.Workflow.Engine.Expressions;

namespace FactoryOS.Plugins.Workflow.Approvals.Domain;

/// <summary>
/// An auto-decision rule: when its condition holds against the approval's context values, the approval
/// short-circuits to the rule's outcome without asking any participant (for example, auto-approve a request
/// under a threshold: <c>amount &lt; 100</c> ⇒ Approved). Rules are evaluated in order at start; the first
/// match wins.
/// </summary>
public sealed class ApprovalRule
{
    private readonly WorkflowExpression _condition;

    /// <summary>Initializes a new instance of the <see cref="ApprovalRule"/> class.</summary>
    /// <param name="condition">The boolean condition expression.</param>
    /// <param name="outcome">The outcome to apply when the condition holds.</param>
    public ApprovalRule(string condition, ApprovalOutcome outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(condition);
        if (outcome == ApprovalOutcome.Pending)
        {
            throw new ArgumentException("An auto-decision rule must yield a terminal outcome.", nameof(outcome));
        }

        Condition = condition;
        Outcome = outcome;
        _condition = WorkflowExpression.Parse(condition);
    }

    /// <summary>Gets the condition expression text.</summary>
    public string Condition { get; }

    /// <summary>Gets the outcome applied when the condition holds.</summary>
    public ApprovalOutcome Outcome { get; }

    /// <summary>Determines whether the rule matches a set of values.</summary>
    /// <param name="values">The approval context values.</param>
    /// <returns><see langword="true"/> when the condition holds.</returns>
    public bool Matches(IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return _condition.EvaluateBoolean(values);
    }
}
