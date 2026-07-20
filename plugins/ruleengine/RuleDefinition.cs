using FactoryOS.Plugins.RuleEngine.Domain;

namespace FactoryOS.Plugins.RuleEngine;

/// <summary>
/// One declarative rule: when the named <see cref="Metric"/> is observed and the value stands in the
/// <see cref="Operator"/> relation to <see cref="Threshold"/>, request <see cref="Action"/>. A rule is pure
/// data — a factory adds, tunes or removes automation by editing configuration, never by touching code.
/// </summary>
public sealed record RuleDefinition
{
    /// <summary>The rule's stable id (for example <c>overtemp-press-1</c>).</summary>
    public required string Id { get; init; }

    /// <summary>The Standard Model metric this rule watches (matched case-insensitively).</summary>
    public required string Metric { get; init; }

    /// <summary>The comparison applied between the observed value and the threshold.</summary>
    public ComparisonOperator Operator { get; init; }

    /// <summary>The threshold the observed value is compared against.</summary>
    public decimal Threshold { get; init; }

    /// <summary>The action to request when the rule matches (for example <c>RaiseMaintenanceAlert</c>).</summary>
    public required string Action { get; init; }
}
