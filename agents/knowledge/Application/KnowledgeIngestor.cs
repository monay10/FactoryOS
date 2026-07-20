using FactoryOS.Ai.Knowledge;
using FactoryOS.Contracts.Ai;

namespace FactoryOS.Agents.Knowledge.Application;

/// <summary>
/// The agent's single ingest path: turn a normalized <see cref="KnowledgeSignal"/> into a
/// <see cref="KnowledgeDocument"/> and push it through the <see cref="IKnowledgeIndexer"/> (chunk → embed → store).
/// Embedding is reached only through the gateway the indexer owns — HTTP to a provider, never an in-process model.
/// This is the bridge that turns the live event stream into knowledge the Company Brain can retrieve and cite.
/// </summary>
/// <remarks>
/// Idempotency is by construction: the signal's <c>Source</c> is derived from the producing event's id, so the
/// indexer's chunk ids (<c>Source#i</c>) are stable and storage upserts rather than duplicates. A redelivery
/// re-embeds and overwrites the same chunks — no separate processed log is needed. An indexer failure throws so
/// the bus retries and can eventually dead-letter; a fact is not silently lost.
/// </remarks>
public sealed class KnowledgeIngestor
{
    private readonly IKnowledgeIndexer _indexer;
    private readonly KnowledgeIngestOptions _options;

    /// <summary>Initializes a new instance of the <see cref="KnowledgeIngestor"/> class.</summary>
    /// <param name="indexer">The knowledge indexer — the door to embedding and storage.</param>
    /// <param name="options">The agent options.</param>
    public KnowledgeIngestor(IKnowledgeIndexer indexer, KnowledgeIngestOptions options)
    {
        ArgumentNullException.ThrowIfNull(indexer);
        ArgumentNullException.ThrowIfNull(options);
        _indexer = indexer;
        _options = options;
    }

    /// <summary>Ingests a signal into the tenant's knowledge base; safe to repeat under at-least-once delivery.</summary>
    /// <param name="signal">The normalized fact to remember.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="InvalidOperationException">Thrown when indexing fails, so the bus can retry.</exception>
    public async Task IngestAsync(KnowledgeSignal signal, CancellationToken cancellationToken)
    {
        var document = new KnowledgeDocument
        {
            Tenant = signal.Tenant,
            Source = signal.Source,
            Text = signal.Text,
        };

        var result = await _indexer.IngestAsync(document, _options.EmbeddingModel, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"Knowledge ingest failed for {signal.Source}: {result.Error.Code} {result.Error.Description}");
        }
    }
}
