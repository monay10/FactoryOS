using FactoryOS.Connectors.Deduplication;
using FactoryOS.Connectors.Normalization;
using FactoryOS.Contracts.Connectors;

namespace FactoryOS.Connectors.Pipeline;

/// <summary>
/// Default <see cref="IIngestionPipeline"/>: streams raw records from the connector, normalizes each
/// through the mapping (collecting rather than throwing on per-record failures) and deduplicates the
/// successful records by natural key.
/// </summary>
public sealed class IngestionPipeline : IIngestionPipeline
{
    private readonly IRecordNormalizer _normalizer;
    private readonly IRecordDeduplicator _deduplicator;

    /// <summary>Initializes a new instance of the <see cref="IngestionPipeline"/> class.</summary>
    /// <param name="normalizer">The normalizer that maps source records into the Standard Model.</param>
    /// <param name="deduplicator">The deduplicator that collapses repeated records.</param>
    public IngestionPipeline(IRecordNormalizer normalizer, IRecordDeduplicator deduplicator)
    {
        ArgumentNullException.ThrowIfNull(normalizer);
        ArgumentNullException.ThrowIfNull(deduplicator);
        _normalizer = normalizer;
        _deduplicator = deduplicator;
    }

    /// <inheritdoc />
    public async Task<IngestionResult> RunAsync(
        IConnector connector,
        MappingManifest mapping,
        ConnectorReadContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connector);
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentNullException.ThrowIfNull(context);

        var read = 0;
        var normalized = new List<NormalizedRecord>();
        var errors = new List<string>();

        await foreach (var record in connector.ReadAsync(context, cancellationToken).ConfigureAwait(false))
        {
            read++;

            var result = _normalizer.Normalize(record, mapping, context.Tenant);
            if (result.IsSuccess)
            {
                normalized.Add(result.Value);
            }
            else
            {
                errors.Add($"{record.SourceEntity}: {result.Error.Code} — {result.Error.Description}");
            }
        }

        var deduplicated = _deduplicator.Deduplicate(normalized);

        return new IngestionResult(deduplicated, read, normalized.Count, deduplicated.Count, errors);
    }
}
