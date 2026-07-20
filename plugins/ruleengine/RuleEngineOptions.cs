namespace FactoryOS.Plugins.RuleEngine;

/// <summary>
/// Configuration for the Rule Engine. The set of rules is data, never a customer branch: a factory declares
/// which metric thresholds should trigger which actions, purely by configuration.
/// </summary>
public sealed record RuleEngineOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Modules:RuleEngine";

    /// <summary>The declarative rules to evaluate against each observed reading.</summary>
    public IReadOnlyList<RuleDefinition> Rules { get; init; } = [];
}
