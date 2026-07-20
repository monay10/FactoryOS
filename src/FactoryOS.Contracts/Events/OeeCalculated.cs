namespace FactoryOS.Contracts.Events;

/// <summary>
/// The shared fact that OEE was computed for a machine-period. Availability, Performance and Quality are
/// fractions in <c>[0, 1]</c> and <c>Oee</c> is their product. Dashboards, reporting and AI agents consume it
/// without referencing the OEE module.
/// </summary>
public sealed record OeeCalculated : IntegrationEvent
{
    /// <summary>The tenant the machine belongs to.</summary>
    public required string Tenant { get; init; }

    /// <summary>The machine OEE was computed for.</summary>
    public required string MachineId { get; init; }

    /// <summary>The start of the reporting period.</summary>
    public DateTimeOffset PeriodStart { get; init; }

    /// <summary>The end of the reporting period.</summary>
    public DateTimeOffset PeriodEnd { get; init; }

    /// <summary>Availability = run time / planned time, in <c>[0, 1]</c>.</summary>
    public decimal Availability { get; init; }

    /// <summary>Performance = (ideal cycle time × total count) / run time, clamped to <c>[0, 1]</c>.</summary>
    public decimal Performance { get; init; }

    /// <summary>Quality = good count / total count, in <c>[0, 1]</c>.</summary>
    public decimal Quality { get; init; }

    /// <summary>Overall Equipment Effectiveness = Availability × Performance × Quality.</summary>
    public decimal Oee { get; init; }

    /// <summary>Whether <see cref="Oee"/> met or exceeded the configured target.</summary>
    public bool MeetsTarget { get; init; }
}
