namespace FactoryOS.Plugins.Production;

/// <summary>
/// Configuration for the Production module. Behaviour varies by configuration, never by customer branch: a
/// factory decides whether counts beyond an order's target keep accruing (over-production) or are capped once
/// the target is reached.
/// </summary>
public sealed record ProductionOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Modules:Production";

    /// <summary>
    /// When <see langword="true"/> (default), counts continue to accrue past the target and
    /// <c>TotalProduced</c> may exceed it. When <see langword="false"/>, the running total is capped at the
    /// target and further counts on a completed order are ignored.
    /// </summary>
    public bool AllowOverProduction { get; init; } = true;
}
