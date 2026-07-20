namespace FactoryOS.Plugins.Oee;

/// <summary>
/// Configuration for the OEE module. Behaviour varies by configuration, never by customer branch: a factory
/// sets its world-class target here (the classic benchmark is 0.85).
/// </summary>
public sealed record OeeOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Modules:Oee";

    /// <summary>The OEE target a machine-period is measured against, as a fraction in <c>[0, 1]</c>.</summary>
    public decimal TargetOee { get; init; } = 0.85m;
}
