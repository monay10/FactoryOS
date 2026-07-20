namespace FactoryOS.Contracts.Events;

/// <summary>
/// Emitted by the Energy module when a meter reading exceeds its rolling baseline by at least the configured
/// threshold. It is shared vocabulary on the bus: Maintenance, Notification and AI agents react to it without
/// any reference to the Energy module.
/// </summary>
public sealed record EnergySpikeDetected : IntegrationEvent
{
    /// <summary>The tenant the spike was detected for.</summary>
    public required string Tenant { get; init; }

    /// <summary>The meter the reading came from.</summary>
    public required string MeterId { get; init; }

    /// <summary>The measured metric.</summary>
    public required string Metric { get; init; }

    /// <summary>The reading value that triggered the spike.</summary>
    public decimal Value { get; init; }

    /// <summary>The baseline the value was compared against.</summary>
    public decimal Baseline { get; init; }

    /// <summary>How far above the baseline the value is, in percent.</summary>
    public decimal DeltaPercent { get; init; }

    /// <summary>The unit of measure.</summary>
    public required string Unit { get; init; }

    /// <summary>The instant the measurement was taken.</summary>
    public DateTimeOffset ReadingAt { get; init; }
}
