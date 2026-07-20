namespace FactoryOS.Plugins.Energy.Domain;

/// <summary>
/// The Energy module's tenant-scoped read model: the write side (fed by the meter-reading handler) records the
/// latest reading and rolling baseline per meter and pushes detected spikes onto a bounded feed; the read side
/// (a dashboard) queries them. This is the CQRS read model the Energy screen renders, kept current purely by
/// consuming the event bus — no module ever queries the Energy module directly.
/// </summary>
public interface IEnergyReadModel
{
    /// <summary>Records a meter's latest reading and baseline for a tenant, replacing any previous entry.</summary>
    /// <param name="reading">The meter reading snapshot.</param>
    void RecordReading(EnergyMeterReading reading);

    /// <summary>Pushes a detected spike onto a tenant's feed, dropping the oldest when at capacity.</summary>
    /// <param name="spike">The spike entry.</param>
    void RecordSpike(EnergySpikeEntry spike);

    /// <summary>The latest reading per meter for a tenant, ordered by meter then metric.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The per-meter latest readings.</returns>
    IReadOnlyList<EnergyMeterReading> Meters(string tenant);

    /// <summary>A tenant's most recent spikes, newest first.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <param name="max">The maximum number to return.</param>
    /// <returns>The recent spikes.</returns>
    IReadOnlyList<EnergySpikeEntry> Spikes(string tenant, int max);

    /// <summary>A tenant's energy headline: meters tracked and spikes retained.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The summary.</returns>
    EnergyReadModelSummary Summarize(string tenant);
}

/// <summary>One meter's latest reading and the baseline it was compared against.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="MeterId">The meter.</param>
/// <param name="Metric">The measured metric.</param>
/// <param name="Value">The latest reading value.</param>
/// <param name="Baseline">The rolling baseline at that reading.</param>
/// <param name="Unit">The unit of measure.</param>
/// <param name="ReadingAt">When the reading was taken.</param>
public readonly record struct EnergyMeterReading(
    string Tenant,
    string MeterId,
    string Metric,
    decimal Value,
    decimal Baseline,
    string Unit,
    DateTimeOffset ReadingAt);

/// <summary>One detected spike, retained for the tenant's recent-spike feed.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="MeterId">The meter.</param>
/// <param name="Metric">The measured metric.</param>
/// <param name="Value">The reading that tripped the spike.</param>
/// <param name="Baseline">The baseline it was compared against.</param>
/// <param name="DeltaPercent">How far above the baseline the value is, in percent.</param>
/// <param name="Unit">The unit of measure.</param>
/// <param name="ReadingAt">When the reading was taken.</param>
public readonly record struct EnergySpikeEntry(
    string Tenant,
    string MeterId,
    string Metric,
    decimal Value,
    decimal Baseline,
    decimal DeltaPercent,
    string Unit,
    DateTimeOffset ReadingAt);

/// <summary>A tenant's energy read-model headline.</summary>
/// <param name="Tenant">The tenant summarized.</param>
/// <param name="Meters">How many meters are tracked.</param>
/// <param name="Spikes">How many spikes are retained in the feed.</param>
public sealed record EnergyReadModelSummary(string Tenant, int Meters, int Spikes);
