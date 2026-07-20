using FactoryOS.Contracts.Connectors;

namespace FactoryOS.Connectors.Pipeline;

/// <summary>
/// Orchestrates the connector ingestion path: read raw records from a connector, normalize each into the
/// Standard Model with a mapping manifest, and deduplicate the result. This is the single pipeline every
/// connector's data flows through before it reaches the event bus.
/// </summary>
public interface IIngestionPipeline
{
    /// <summary>Reads from a connector, normalizes and deduplicates, and returns the result.</summary>
    /// <param name="connector">The connector to read raw records from.</param>
    /// <param name="mapping">The mapping manifest that drives normalization.</param>
    /// <param name="context">The read context carrying the tenant and read parameters.</param>
    /// <param name="cancellationToken">A token to cancel the run.</param>
    /// <returns>The ingestion result: deduplicated records, counts and any normalization errors.</returns>
    Task<IngestionResult> RunAsync(
        IConnector connector,
        MappingManifest mapping,
        ConnectorReadContext context,
        CancellationToken cancellationToken);
}
