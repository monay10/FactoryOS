using FactoryOS.Ai.Gateway;
using FactoryOS.Ai.Knowledge;
using FactoryOS.Contracts.Ai;
using FactoryOS.Domain.Results;

namespace FactoryOS.Tests.Ai;

public sealed class RagTests
{
    private sealed class MismatchGateway : IEmbeddingGateway
    {
        public Task<Result<EmbeddingResponse>> EmbedAsync(EmbeddingRequest request, CancellationToken cancellationToken)
            => Task.FromResult(Result.Success(new EmbeddingResponse
            {
                Model = request.Model,
                Vectors = [[1f, 0f, 0f]], // one vector regardless of how many inputs
            }));
    }

    private static (KnowledgeIndexer Indexer, KnowledgeRetriever Retriever, InMemoryKnowledgeStore Store) BuildPipeline()
    {
        var gateway = new FakeEmbeddingGateway(FakeEmbeddingGateway.KeywordEmbed);
        var store = new InMemoryKnowledgeStore();
        return (new KnowledgeIndexer(gateway, store), new KnowledgeRetriever(gateway, store), store);
    }

    [Fact]
    public async Task Index_then_retrieve_returns_the_most_relevant_chunk_first()
    {
        var (indexer, retriever, _) = BuildPipeline();
        await indexer.IngestAsync(
            new KnowledgeDocument { Tenant = "acme", Source = "pump-manual", Text = "The centrifugal pump needs monthly lubrication." },
            "embed", CancellationToken.None);
        await indexer.IngestAsync(
            new KnowledgeDocument { Tenant = "acme", Source = "boiler-manual", Text = "The boiler relief valve must be tested yearly." },
            "embed", CancellationToken.None);

        var result = await retriever.RetrieveAsync("acme", "pump maintenance", "embed", topK: 2, CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.NotEmpty(result.Value);
        Assert.Equal("pump-manual", result.Value[0].Chunk.Source);
        Assert.Equal(1.0, result.Value[0].Score, 6);
    }

    [Fact]
    public async Task Retrieval_is_tenant_scoped()
    {
        var (indexer, retriever, _) = BuildPipeline();
        await indexer.IngestAsync(
            new KnowledgeDocument { Tenant = "acme", Source = "pump-manual", Text = "pump pump pump" },
            "embed", CancellationToken.None);

        var result = await retriever.RetrieveAsync("other", "pump", "embed", topK: 5, CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task Ingesting_an_empty_document_stores_nothing()
    {
        var (indexer, _, store) = BuildPipeline();

        var result = await indexer.IngestAsync(
            new KnowledgeDocument { Tenant = "acme", Source = "blank", Text = "   " },
            "embed", CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal(0, result.Value);
        var search = await store.SearchAsync("acme", [1f, 0f, 0f], topK: 5, CancellationToken.None);
        Assert.Empty(search.Value);
    }

    [Fact]
    public async Task Ingest_fails_when_the_provider_returns_the_wrong_vector_count()
    {
        var indexer = new KnowledgeIndexer(new MismatchGateway(), new InMemoryKnowledgeStore());

        var result = await indexer.IngestAsync(
            new KnowledgeDocument
            {
                Tenant = "acme",
                Source = "big",
                Text = string.Join(' ', Enumerable.Range(0, 400).Select(i => $"word{i}")),
            },
            "embed", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Ai.Knowledge.EmbeddingCountMismatch", result.Error.Code);
    }

    [Fact]
    public void RagContext_renders_numbered_source_attributed_chunks()
    {
        var chunks = new List<ScoredChunk>
        {
            new() { Chunk = new KnowledgeChunk { Id = "pump#0", Tenant = "acme", Source = "pump-manual", Text = "lubricate monthly" }, Score = 0.9 },
            new() { Chunk = new KnowledgeChunk { Id = "boiler#0", Tenant = "acme", Source = "boiler-manual", Text = "test valve" }, Score = 0.4 },
        };

        var context = RagContext.Build(chunks);

        Assert.Contains("[1] (pump-manual) lubricate monthly", context, StringComparison.Ordinal);
        Assert.Contains("[2] (boiler-manual) test valve", context, StringComparison.Ordinal);
        Assert.Equal("", RagContext.Build([]));
    }
}
