namespace FactoryOS.Plugins.Carbon.Domain;

/// <summary>
/// Converts energy to a carbon-equivalent emission. Pure and deterministic — no state, no I/O — so it is fully
/// offline-testable.
/// </summary>
public static class CarbonCalculator
{
    /// <summary>Computes the emission for an energy value under an emission factor.</summary>
    /// <param name="energyValue">The energy value.</param>
    /// <param name="emissionFactor">The emission factor in kg CO₂e per energy unit.</param>
    /// <returns>The emission in kg CO₂e.</returns>
    public static decimal Co2eKg(decimal energyValue, decimal emissionFactor) => energyValue * emissionFactor;
}
