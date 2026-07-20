namespace FactoryOS.Plugins.Oee.Domain;

/// <summary>A stored OEE result for one machine-period — the module's read-model row.</summary>
/// <param name="Tenant">The owning tenant.</param>
/// <param name="MachineId">The machine.</param>
/// <param name="PeriodStart">The period start.</param>
/// <param name="PeriodEnd">The period end.</param>
/// <param name="Score">The computed factors and OEE.</param>
public sealed record OeeSnapshot(
    string Tenant,
    string MachineId,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    OeeScore Score);
