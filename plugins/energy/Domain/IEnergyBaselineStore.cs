namespace FactoryOS.Plugins.Energy.Domain;

/// <summary>
/// Tracks a rolling baseline per meter aggregate. Observing a value returns the baseline as it stood before
/// that value, then folds the value into the window. Tenant-scoped by the key; no aggregate sees another's.
/// </summary>
public interface IEnergyBaselineStore
{
    /// <summary>Records a reading and returns the baseline that stood immediately before it.</summary>
    /// <param name="key">The meter aggregate.</param>
    /// <param name="value">The reading value to fold into the rolling window.</param>
    /// <returns>The prior baseline snapshot (before <paramref name="value"/> was added).</returns>
    BaselineSnapshot Observe(EnergyMeterKey key, decimal value);
}
