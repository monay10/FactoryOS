using FactoryOS.Plugins.Workflow.Engine.Assignments;
using FactoryOS.Plugins.Workflow.Engine.Domain;
using FactoryOS.Plugins.Workflow.Engine.Expressions;

namespace FactoryOS.Plugins.Workflow.Engine.Nodes;

/// <summary>The base of a workflow node: a stable id, a display name and the kind that drives execution.</summary>
/// <param name="Id">The node id, unique within a definition.</param>
/// <param name="Name">The display name.</param>
/// <param name="Kind">The node kind.</param>
public abstract record WorkflowNode(string Id, string Name, WorkflowNodeKind Kind);

/// <summary>The single entry point of a definition.</summary>
/// <param name="Id">The node id.</param>
/// <param name="Name">The display name.</param>
public sealed record StartNode(string Id, string Name = "Start") : WorkflowNode(Id, Name, WorkflowNodeKind.Start);

/// <summary>A terminal node; the instance completes when no tokens remain.</summary>
/// <param name="Id">The node id.</param>
/// <param name="Name">The display name.</param>
public sealed record EndNode(string Id, string Name = "End") : WorkflowNode(Id, Name, WorkflowNodeKind.End);

/// <summary>A human/task activity that waits for external completion, optionally assigned to someone.</summary>
/// <param name="Id">The node id.</param>
/// <param name="Name">The display name.</param>
/// <param name="ActivityKey">The activity type key.</param>
/// <param name="Assignment">The optional assignment.</param>
public sealed record ActivityNode(string Id, string Name, string ActivityKey, WorkflowAssignment? Assignment = null)
    : WorkflowNode(Id, Name, WorkflowNodeKind.Activity);

/// <summary>An exclusive gateway; the executor follows the first outgoing transition whose condition holds.</summary>
/// <param name="Id">The node id.</param>
/// <param name="Name">The display name.</param>
public sealed record DecisionNode(string Id, string Name = "Decision")
    : WorkflowNode(Id, Name, WorkflowNodeKind.Decision);

/// <summary>An AND-split that activates every outgoing transition.</summary>
/// <param name="Id">The node id.</param>
/// <param name="Name">The display name.</param>
public sealed record ParallelNode(string Id, string Name = "Parallel")
    : WorkflowNode(Id, Name, WorkflowNodeKind.Parallel);

/// <summary>An AND-join that waits for every incoming branch before continuing.</summary>
/// <param name="Id">The node id.</param>
/// <param name="Name">The display name.</param>
public sealed record MergeNode(string Id, string Name = "Merge") : WorkflowNode(Id, Name, WorkflowNodeKind.Merge);

/// <summary>A node that waits until a due time relative to arrival.</summary>
/// <param name="Id">The node id.</param>
/// <param name="Delay">The delay before the timer fires.</param>
/// <param name="Name">The display name.</param>
public sealed record TimerNode(string Id, TimeSpan Delay, string Name = "Timer")
    : WorkflowNode(Id, Name, WorkflowNodeKind.Timer);

/// <summary>A node that waits until a named external signal arrives.</summary>
/// <param name="Id">The node id.</param>
/// <param name="SignalName">The signal name that resumes the node.</param>
/// <param name="Name">The display name.</param>
public sealed record WaitNode(string Id, string SignalName, string Name = "Wait")
    : WorkflowNode(Id, Name, WorkflowNodeKind.Wait);

/// <summary>A single variable assignment applied by a <see cref="ScriptNode"/>.</summary>
public sealed record ScriptAssignment
{
    private readonly WorkflowExpression _expression;

    /// <summary>Initializes a new instance of the <see cref="ScriptAssignment"/> record.</summary>
    /// <param name="variable">The variable to assign.</param>
    /// <param name="expression">The value expression.</param>
    public ScriptAssignment(string variable, string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(variable);
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        Variable = variable;
        _expression = WorkflowExpression.Parse(expression);
    }

    /// <summary>Gets the target variable name.</summary>
    public string Variable { get; }

    /// <summary>Evaluates the assignment's value against the variables.</summary>
    /// <param name="variables">The instance variables.</param>
    /// <returns>The value to assign.</returns>
    public object? Evaluate(WorkflowVariables variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        return _expression.Evaluate(variables.AsReadOnly());
    }
}

/// <summary>A node that applies variable assignments computed from expressions, then continues.</summary>
/// <param name="Id">The node id.</param>
/// <param name="Assignments">The assignments to apply.</param>
/// <param name="Name">The display name.</param>
public sealed record ScriptNode(string Id, IReadOnlyList<ScriptAssignment> Assignments, string Name = "Script")
    : WorkflowNode(Id, Name, WorkflowNodeKind.Script);

/// <summary>A node that invokes a registered in-process service by key, then continues.</summary>
/// <param name="Id">The node id.</param>
/// <param name="ServiceKey">The service key to invoke.</param>
/// <param name="Name">The display name.</param>
public sealed record ServiceNode(string Id, string ServiceKey, string Name = "Service")
    : WorkflowNode(Id, Name, WorkflowNodeKind.Service);
