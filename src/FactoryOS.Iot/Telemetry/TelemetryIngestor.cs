using FactoryOS.Contracts.Iot;
using FactoryOS.Contracts.StandardModel;

namespace FactoryOS.Iot.Telemetry;

/// <summary>
/// Default <see cref="ITelemetryIngestor"/>: normalizes each sample through the
/// <see cref="ITelemetryNormalizer"/>, collecting successes as readings and failures as errors so one bad
/// sample never fails the batch.
/// </summary>
public sealed class TelemetryIngestor : ITelemetryIngestor
{
    private readonly ITelemetryNormalizer _normalizer;

    /// <summary>Initializes a new instance of the <see cref="TelemetryIngestor"/> class.</summary>
    /// <param name="normalizer">The normalizer that turns samples into meter readings.</param>
    public TelemetryIngestor(ITelemetryNormalizer normalizer)
    {
        ArgumentNullException.ThrowIfNull(normalizer);
        _normalizer = normalizer;
    }

    /// <inheritdoc />
    public TelemetryIngestionResult Ingest(IEnumerable<TelemetrySample> samples, string tenant)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        var read = 0;
        var readings = new List<MeterReading>();
        var errors = new List<string>();

        foreach (var sample in samples)
        {
            read++;

            var result = _normalizer.Normalize(sample, tenant);
            if (result.IsSuccess)
            {
                readings.Add(result.Value);
            }
            else
            {
                errors.Add($"{sample.DeviceId}/{sample.Tag}: {result.Error.Code} — {result.Error.Description}");
            }
        }

        return new TelemetryIngestionResult(readings, read, errors);
    }
}
