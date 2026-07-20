namespace FactoryOS.Plugins.Quality.Domain;

/// <summary>
/// A read-model row for one line-product aggregate: its identity plus the current rolling defect-rate window.
/// The read API projects these; the alerting handler is unchanged and still decides breaches from the same window.
/// </summary>
/// <param name="Tenant">The owning tenant.</param>
/// <param name="LineId">The production line or workstation identifier.</param>
/// <param name="ProductId">The product identifier.</param>
/// <param name="Window">The current rolling window aggregate for the aggregate.</param>
public sealed record QualityLineSnapshot(
    string Tenant,
    string LineId,
    string ProductId,
    DefectRateSnapshot Window);
