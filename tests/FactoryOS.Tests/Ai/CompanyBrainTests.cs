using FactoryOS.Ai.Brain;
using FactoryOS.Ai.Knowledge;
using FactoryOS.Ai.Prompts;
using FactoryOS.Contracts.Ai;

namespace FactoryOS.Tests.Ai;

public sealed class CompanyBrainTests
{
    private sealed record Harness(CompanyBrain Brain, KnowledgeIndexer Indexer, FakeLlmGateway Llm);

    private static Harness BuildBrain(string answer = "canned answer")
    {
        var embeddings = new FakeEmbeddingGateway(FakeEmbeddingGateway.KeywordEmbed);
        var store = new InMemoryKnowledgeStore();
        var indexer = new KnowledgeIndexer(embeddings, store);
        var retriever = new KnowledgeRetriever(embeddings, store);

        var catalog = new InMemoryPromptCatalog();
        catalog.Register(BrainPrompts.Answer);
        var composer = new PromptComposer(catalog, new PromptRenderer());

        var llm = new FakeLlmGateway(answer);
        return new Harness(new CompanyBrain(retriever, composer, llm), indexer, llm);
    }

    private static BrainQuestion Ask(string question) => new()
    {
        Tenant = "acme",
        Question = question,
        ChatModel = "fast",
        EmbeddingModel = "embed",
        TopK = 3,
    };

    [Fact]
    public async Task Answers_grounded_in_retrieved_knowledge_with_citations()
    {
        var harness = BuildBrain("Lubricate the pump bearings monthly.");
        await harness.Indexer.IngestAsync(
            new KnowledgeDocument { Tenant = "acme", Source = "pump-manual", Text = "The centrifugal pump needs monthly lubrication." },
            "embed", CancellationToken.None);

        var result = await harness.Brain.AskAsync(Ask("how often to lubricate the pump?"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal("Lubricate the pump bearings monthly.", result.Value.Answer);
        Assert.Equal("fast-upstream", result.Value.Model);
        var citation = Assert.Single(result.Value.Citations);
        Assert.Equal("pump-manual", citation.Source);
        Assert.Equal("pump-manual#0", citation.ChunkId);
    }

    [Fact]
    public async Task Injects_the_retrieved_context_into_the_prompt_the_model_sees()
    {
        var harness = BuildBrain();
        await harness.Indexer.IngestAsync(
            new KnowledgeDocument { Tenant = "acme", Source = "pump-manual", Text = "The centrifugal pump needs monthly lubrication." },
            "embed", CancellationToken.None);

        await harness.Brain.AskAsync(Ask("pump lubrication?"), CancellationToken.None);

        Assert.Contains("(pump-manual)", harness.Llm.LastPromptText, StringComparison.Ordinal);
        Assert.Contains("monthly lubrication", harness.Llm.LastPromptText, StringComparison.Ordinal);
        Assert.Equal("acme", harness.Llm.LastRequest!.Tenant); // tenant flows through to generation
    }

    [Fact]
    public async Task Answers_with_no_citations_when_nothing_is_retrieved()
    {
        var harness = BuildBrain("I don't have that in the knowledge base.");

        var result = await harness.Brain.AskAsync(Ask("what is the boiler pressure?"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Empty(result.Value.Citations);
        Assert.Equal("I don't have that in the knowledge base.", result.Value.Answer);
    }

    [Fact]
    public async Task Retrieval_and_generation_stay_within_the_asking_tenant()
    {
        var harness = BuildBrain();
        await harness.Indexer.IngestAsync(
            new KnowledgeDocument { Tenant = "other", Source = "secret", Text = "The pump code is 1234." },
            "embed", CancellationToken.None);

        var result = await harness.Brain.AskAsync(Ask("pump code?"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Empty(result.Value.Citations); // the other tenant's chunk is unreachable
        Assert.DoesNotContain("1234", harness.Llm.LastPromptText, StringComparison.Ordinal);
    }
}
