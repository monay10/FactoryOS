namespace FactoryOS.Connectors.Logo;

/// <summary>
/// Strongly-typed configuration for the Logo connector. A new Logo company is configuration only: the
/// firm and period numbers that select the right Logo tables.
/// </summary>
public sealed record LogoConnectorOptions
{
    /// <summary>Gets the Logo firm number whose item master is read.</summary>
    public required int FirmNumber { get; init; }

    /// <summary>Gets the Logo period number whose stock totals supply on-hand quantities.</summary>
    public required int PeriodNumber { get; init; }
}
