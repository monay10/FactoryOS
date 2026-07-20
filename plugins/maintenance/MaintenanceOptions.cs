namespace FactoryOS.Plugins.Maintenance;

/// <summary>
/// Configuration for the Maintenance module. Behaviour varies by configuration, never by customer branch: a
/// factory tunes how spike-triggered work orders are numbered and scheduled here.
/// </summary>
public sealed record MaintenanceOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Modules:Maintenance";

    /// <summary>How many hours ahead a spike-triggered corrective work order is due.</summary>
    public int SpikeWorkOrderDueInHours { get; init; } = 24;

    /// <summary>The prefix for generated work-order numbers.</summary>
    public string SpikeWorkOrderPrefix { get; init; } = "WO";

    /// <summary>How many hours ahead a rule-triggered corrective work order is due.</summary>
    public int RuleWorkOrderDueInHours { get; init; } = 24;

    /// <summary>The prefix for rule-triggered work-order numbers.</summary>
    public string RuleWorkOrderPrefix { get; init; } = "WOR";

    /// <summary>
    /// The set of <c>RuleTriggered</c> actions the module raises a work order for (matched case-insensitively).
    /// A rule requesting any other action is ignored here — the module reacts to data, never to a customer branch.
    /// </summary>
    public IReadOnlyCollection<string> RuleActions { get; init; } = ["RaiseMaintenanceAlert"];
}
