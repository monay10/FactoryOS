namespace FactoryOS.Plugins.Workflow.Domain;

/// <summary>
/// Resolves the workflow rule that matches a trigger type. Built from configuration; the lookup is data-driven,
/// never a branch on the trigger name in code.
/// </summary>
public interface IWorkflowRuleSet
{
    /// <summary>Resolves the rule for a trigger type, if one is configured.</summary>
    /// <param name="triggerType">The trigger event type name.</param>
    /// <returns>The matching rule, or <see langword="null"/> if no rule is configured for the trigger.</returns>
    WorkflowRule? Resolve(string triggerType);
}
