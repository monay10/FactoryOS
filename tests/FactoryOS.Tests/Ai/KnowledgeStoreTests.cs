using FactoryOS.Ai.Knowledge;
using FactoryOS.Contracts.Ai;

namespace FactoryOS.Tests.Ai;

public sealed class KnowledgeStoreTests
{
    private static EmbeddedChunk Chunk(string tenant, string id, params float[] vector) => new(
        new KnowledgeChunk { Id = id, Tenant = tenant, Source = "doc", Ordinal = 0, Text = id },
        vector);

    [Fact]
    public async Task Search_ranks_by_cosine_similarity_and_respects_top_k()
    {
        var store = new InMemoryKnowledgeStore();
        await store.UpsertAsync("acme",
        [
            Chunk("acme", "near", 1f, 0.1f),
            Chunk("acme", "far", 0f, 1f),
            Chunk("acme", "mid", 0.7f, 0.7f),
        ], CancellationToken.None);

        var result = await store.SearchAsync("acme", [1f, 0f], topK: 2, CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal("near", result.Value[0].Chunk.Id);
        Assert.True(result.Value[0].Score > result.Value[1].Score);
    }

    [Fact]
    public async Task Tenants_are_isolated()
    {
        var store = new InMemoryKnowledgeStore();
        await store.UpsertAsync("acme", [Chunk("acme", "a", 1f, 0f)], CancellationToken.None);

        var result = await store.SearchAsync("other", [1f, 0f], topK: 5, CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Empty(result.Value); // nothing leaks across the tenant boundary
    }

    [Fact]
    public async Task Upsert_rejects_a_chunk_tagged_for_a_different_tenant()
    {
        var store = new InMemoryKnowledgeStore();

        var result = await store.UpsertAsync("acme", [Chunk("intruder", "x", 1f, 0f)], CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Ai.Knowledge.TenantMismatch", result.Error.Code);
    }

    [Fact]
    public async Task Upsert_replaces_a_chunk_with_the_same_id()
    {
        var store = new InMemoryKnowledgeStore();
        await store.UpsertAsync("acme", [Chunk("acme", "a", 1f, 0f)], CancellationToken.None);
        await store.UpsertAsync("acme", [Chunk("acme", "a", 0f, 1f)], CancellationToken.None);

        var result = await store.SearchAsync("acme", [0f, 1f], topK: 5, CancellationToken.None);

        Assert.Single(result.Value); // still one chunk, now the replacement
        Assert.Equal(1.0, result.Value[0].Score, 6);
    }

    [Fact]
    public async Task Search_fails_for_a_non_positive_top_k()
    {
        var store = new InMemoryKnowledgeStore();

        var result = await store.SearchAsync("acme", [1f, 0f], topK: 0, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Ai.Knowledge.InvalidTopK", result.Error.Code);
    }
}
