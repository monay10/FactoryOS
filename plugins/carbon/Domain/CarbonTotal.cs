namespace FactoryOS.Plugins.Carbon.Domain;

/// <summary>A read-model snapshot of the cumulative emission tracked for a source.</summary>
/// <param name="Tenant">The owning tenant.</param>
/// <param name="Source">The energy source (meter).</param>
/// <param name="CumulativeCo2eKg">The cumulative emission for the source, in kg CO₂e.</param>
public sealed record CarbonTotal(string Tenant, string Source, decimal CumulativeCo2eKg);
