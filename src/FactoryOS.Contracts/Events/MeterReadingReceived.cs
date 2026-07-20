using FactoryOS.Contracts.StandardModel;

namespace FactoryOS.Contracts.Events;

/// <summary>
/// The canonical fact that a Standard Model <see cref="MeterReading"/> was observed and placed on the bus.
/// It belongs to the shared vocabulary — any producer (the IoT bridge, a connector ingestion job) publishes
/// it and any module (Energy, OEE, …) consumes it, so modules never reference one another. Delivery is
/// at-least-once; consumers deduplicate by <see cref="IIntegrationEvent.EventId"/>.
/// </summary>
public sealed record MeterReadingReceived : IntegrationEvent
{
    /// <summary>The Standard Model reading that was observed.</summary>
    public required MeterReading Reading { get; init; }
}
