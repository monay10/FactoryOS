using FactoryOS.Contracts.Iot;

namespace FactoryOS.Iot.Telemetry;

/// <summary>Normalizes a batch of telemetry samples into Standard Model meter readings.</summary>
public interface ITelemetryIngestor
{
    /// <summary>Ingests a batch of samples for a tenant, collecting per-sample errors rather than throwing.</summary>
    /// <param name="samples">The raw samples to ingest.</param>
    /// <param name="tenant">The tenant the samples belong to.</param>
    /// <returns>The ingestion result: normalized readings, the sample count and any errors.</returns>
    TelemetryIngestionResult Ingest(IEnumerable<TelemetrySample> samples, string tenant);
}
