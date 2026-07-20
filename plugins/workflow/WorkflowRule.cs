namespace FactoryOS.Plugins.Workflow;

/// <summary>
/// A single declarative workflow rule: when an event of <see cref="Trigger"/> type occurs, request
/// <see cref="Action"/> at <see cref="Priority"/> on <see cref="Channel"/>. Rules are data — the tenant supplies
/// them in configuration; the module never branches on a customer or a specific trigger in code.
/// </summary>
public sealed record WorkflowRule
{
    /// <summary>The trigger event type this rule matches (for example <c>SafetyStandDownTriggered</c>).</summary>
    public required string Trigger { get; init; }

    /// <summary>The action to request when the rule matches (for example <c>Notify</c>).</summary>
    public required string Action { get; init; }

    /// <summary>The priority of the requested action.</summary>
    public string Priority { get; init; } = "Normal";

    /// <summary>The channel or audience the action targets.</summary>
    public string Channel { get; init; } = "default";
}
