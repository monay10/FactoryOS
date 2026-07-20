namespace FactoryOS.Contracts.Events;

/// <summary>
/// The shared fact that a machine's production figures for a period are available (a shift rollup, an hourly
/// bucket, …). It carries the raw inputs OEE is computed from. Any module consumes it without referencing the
/// producer; the OEE module turns it into an <see cref="OeeCalculated"/>.
/// </summary>
public sealed record ProductionPeriodReported : IntegrationEvent
{
    /// <summary>The tenant the machine belongs to.</summary>
    public required string Tenant { get; init; }

    /// <summary>The machine the figures are for.</summary>
    public required string MachineId { get; init; }

    /// <summary>The start of the reporting period.</summary>
    public DateTimeOffset PeriodStart { get; init; }

    /// <summary>The end of the reporting period.</summary>
    public DateTimeOffset PeriodEnd { get; init; }

    /// <summary>Planned production time in the period, in seconds (the Availability denominator).</summary>
    public decimal PlannedTimeSeconds { get; init; }

    /// <summary>Actual running time in the period, in seconds (the Availability numerator).</summary>
    public decimal RunTimeSeconds { get; init; }

    /// <summary>The ideal cycle time per unit, in seconds (the Performance basis).</summary>
    public decimal IdealCycleTimeSeconds { get; init; }

    /// <summary>Total units produced in the period.</summary>
    public int TotalCount { get; init; }

    /// <summary>Good (in-spec) units produced in the period (the Quality numerator).</summary>
    public int GoodCount { get; init; }
}
