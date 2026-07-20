using FactoryOS.Contracts.Connectors;

namespace FactoryOS.Connectors.Pipeline;

/// <summary>
/// The outcome of running the ingestion pipeline: the deduplicated Standard Model records plus counts
/// and any per-record normalization errors, for observability.
/// </summary>
/// <param name="Records">The deduplicated, normalized records ready to publish.</param>
/// <param name="Read">The number of raw source records read from the connector.</param>
/// <param name="Normalized">The number of records that normalized successfully, before deduplication.</param>
/// <param name="Deduplicated">The number of records remaining after deduplication.</param>
/// <param name="Errors">Human-readable descriptions of records that failed to normalize.</param>
public sealed record IngestionResult(
    IReadOnlyList<NormalizedRecord> Records,
    int Read,
    int Normalized,
    int Deduplicated,
    IReadOnlyList<string> Errors);
