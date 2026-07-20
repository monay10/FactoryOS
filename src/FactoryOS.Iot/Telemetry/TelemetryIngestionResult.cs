using FactoryOS.Contracts.StandardModel;

namespace FactoryOS.Iot.Telemetry;

/// <summary>
/// The outcome of ingesting a batch of telemetry: the normalized meter readings plus counts and any
/// per-sample errors, for observability.
/// </summary>
/// <param name="Readings">The normalized Standard Model meter readings ready to publish.</param>
/// <param name="Read">The number of raw samples processed.</param>
/// <param name="Errors">Human-readable descriptions of samples that failed to normalize.</param>
public sealed record TelemetryIngestionResult(
    IReadOnlyList<MeterReading> Readings,
    int Read,
    IReadOnlyList<string> Errors);
