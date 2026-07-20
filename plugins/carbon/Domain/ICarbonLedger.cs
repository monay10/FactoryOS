namespace FactoryOS.Plugins.Carbon.Domain;

/// <summary>
/// Accumulates cumulative CO₂e per source, per tenant. Tenant-scoped through the key. Replaceable by an EF Core
/// or Redis read-model behind the interface.
/// </summary>
public interface ICarbonLedger
{
    /// <summary>Adds an emission to a source's running total and returns the new cumulative value.</summary>
    /// <param name="key">The source the emission is for.</param>
    /// <param name="co2eKg">The emission to add, in kg CO₂e.</param>
    /// <returns>The cumulative emission for the source after adding, in kg CO₂e.</returns>
    decimal Accrue(CarbonSourceKey key, decimal co2eKg);

    /// <summary>Returns the cumulative emissions of all sources for a tenant.</summary>
    /// <param name="tenant">The tenant to list within.</param>
    /// <returns>The tenant's per-source cumulative totals.</returns>
    IReadOnlyCollection<CarbonTotal> ForTenant(string tenant);
}
