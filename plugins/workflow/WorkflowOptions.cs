namespace FactoryOS.Plugins.Workflow;

/// <summary>
/// Configuration for the Workflow module. Behaviour varies by configuration, never by customer branch: a factory
/// supplies its escalation rules as data. Adding, removing or retargeting a rule is a config change, never code.
/// </summary>
public sealed record WorkflowOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Modules:Workflow";

    /// <summary>The declarative rules mapping trigger event types to requested actions.</summary>
    public IReadOnlyList<WorkflowRule> Rules { get; init; } = [];
}
