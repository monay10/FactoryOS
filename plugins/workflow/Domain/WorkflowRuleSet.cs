namespace FactoryOS.Plugins.Workflow.Domain;

/// <summary>
/// The default <see cref="IWorkflowRuleSet"/>: an immutable index of the configured rules by trigger type. When
/// two rules target the same trigger, the first configured wins. Pure lookup, no I/O.
/// </summary>
public sealed class WorkflowRuleSet : IWorkflowRuleSet
{
    private readonly Dictionary<string, WorkflowRule> _byTrigger;

    /// <summary>Initializes a new instance of the <see cref="WorkflowRuleSet"/> class from configured rules.</summary>
    /// <param name="options">The module options carrying the rules.</param>
    public WorkflowRuleSet(WorkflowOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var byTrigger = new Dictionary<string, WorkflowRule>(StringComparer.Ordinal);
        foreach (var rule in options.Rules)
        {
            byTrigger.TryAdd(rule.Trigger, rule);
        }

        _byTrigger = byTrigger;
    }

    /// <inheritdoc />
    public WorkflowRule? Resolve(string triggerType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(triggerType);
        return _byTrigger.TryGetValue(triggerType, out var rule) ? rule : null;
    }
}
