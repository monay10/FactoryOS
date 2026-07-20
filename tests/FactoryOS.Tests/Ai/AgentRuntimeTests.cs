using FactoryOS.Ai.Agents;
using FactoryOS.Ai.Knowledge;
using FactoryOS.Ai.Prompts;
using FactoryOS.Contracts.Ai;

namespace FactoryOS.Tests.Ai;

public sealed class AgentRuntimeTests
{
    private sealed record Harness(AgentRuntime Runtime, InMemoryAgentCatalog Catalog, KnowledgeIndexer Indexer, FakeLlmGateway Llm);

    private static Harness Build(string answer = "agent output")
    {
        var embeddings = new FakeEmbeddingGateway(FakeEmbeddingGateway.KeywordEmbed);
        var store = new InMemoryKnowledgeStore();
        var indexer = new KnowledgeIndexer(embeddings, store);
        var retriever = new KnowledgeRetriever(embeddings, store);
        var catalog = new InMemoryAgentCatalog();
        var llm = new FakeLlmGateway(answer);
        return new Harness(new AgentRuntime(catalog, new PromptRenderer(), retriever, llm), catalog, indexer, llm);
    }

    private static AgentRequest Run(string agentKey, string input, IReadOnlyDictionary<string, string>? vars = null) => new()
    {
        Tenant = "acme",
        AgentKey = agentKey,
        Input = input,
        Variables = vars,
    };

    [Fact]
    public async Task Runs_an_ungrounded_agent_from_its_manifest()
    {
        var harness = Build("Escalate to level 2.");
        harness.Catalog.Register(new AgentDefinition
        {
            Key = "maintenance.triage",
            Name = "Triage",
            SystemPrompt = "You triage maintenance requests.",
            ChatModel = "fast",
        });

        var result = await harness.Runtime.RunAsync(Run("maintenance.triage", "Pump is vibrating."), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal("Escalate to level 2.", result.Value.Output);
        Assert.Equal("fast-upstream", result.Value.Model);
        Assert.Empty(result.Value.Grounding);
        Assert.Equal("You triage maintenance requests.", harness.Llm.LastRequest!.Messages[0].Content);
        Assert.Contains("Pump is vibrating.", harness.Llm.LastRequest.Messages[1].Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Grounds_a_grounded_agent_in_the_knowledge_base()
    {
        var harness = Build();
        await harness.Indexer.IngestAsync(
            new KnowledgeDocument { Tenant = "acme", Source = "pump-manual", Text = "The centrifugal pump needs monthly lubrication." },
            "embed", CancellationToken.None);
        harness.Catalog.Register(new AgentDefinition
        {
            Key = "maintenance.advisor",
            Name = "Advisor",
            SystemPrompt = "You advise on maintenance.",
            ChatModel = "fast",
            Grounding = new AgentGrounding { EmbeddingModel = "embed", TopK = 3 },
        });

        var result = await harness.Runtime.RunAsync(Run("maintenance.advisor", "pump service interval?"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Single(result.Value.Grounding);
        Assert.Equal("pump-manual", result.Value.Grounding[0].Chunk.Source);
        Assert.Contains("(pump-manual)", harness.Llm.LastRequest!.Messages[1].Content, StringComparison.Ordinal);
        Assert.Contains("Task: pump service interval?", harness.Llm.LastRequest.Messages[1].Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Binds_system_prompt_variables()
    {
        var harness = Build();
        harness.Catalog.Register(new AgentDefinition
        {
            Key = "greeter",
            Name = "Greeter",
            SystemPrompt = "You reply in {{language}} as a {{role}}.",
            ChatModel = "fast",
        });

        var result = await harness.Runtime.RunAsync(
            Run("greeter", "hi", new Dictionary<string, string> { ["language"] = "Turkish", ["role"] = "operator" }),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal("You reply in Turkish as a operator.", harness.Llm.LastRequest!.Messages[0].Content);
    }

    [Fact]
    public async Task Fails_when_a_system_prompt_variable_is_unbound()
    {
        var harness = Build();
        harness.Catalog.Register(new AgentDefinition
        {
            Key = "greeter",
            Name = "Greeter",
            SystemPrompt = "You reply in {{language}}.",
            ChatModel = "fast",
        });

        var result = await harness.Runtime.RunAsync(Run("greeter", "hi"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Ai.Prompt.MissingVariable", result.Error.Code);
    }

    [Fact]
    public async Task Fails_for_an_unknown_agent()
    {
        var harness = Build();

        var result = await harness.Runtime.RunAsync(Run("ghost", "hello"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Ai.Agent.UnknownAgent", result.Error.Code);
    }
}
