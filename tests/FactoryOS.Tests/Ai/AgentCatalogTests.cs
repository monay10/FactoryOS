using FactoryOS.Ai.Agents;
using FactoryOS.Contracts.Ai;

namespace FactoryOS.Tests.Ai;

public sealed class AgentCatalogTests
{
    private static AgentDefinition Agent(string key, int version) => new()
    {
        Key = key,
        Name = key,
        Version = version,
        SystemPrompt = "system",
        ChatModel = "fast",
    };

    [Fact]
    public void Keeps_the_highest_version_for_a_key()
    {
        var catalog = new InMemoryAgentCatalog();
        catalog.Register(Agent("triage", 1));
        catalog.Register(Agent("triage", 3));
        catalog.Register(Agent("triage", 2)); // lower — ignored

        Assert.True(catalog.TryGet("triage", out var found));
        Assert.Equal(3, found.Version);
    }

    [Fact]
    public void Reports_all_registered_agents()
    {
        var catalog = new InMemoryAgentCatalog();
        catalog.Register(Agent("a", 1));
        catalog.Register(Agent("b", 1));

        Assert.Equal(2, catalog.All.Count);
    }

    [Fact]
    public void TryGet_returns_false_for_an_unknown_key()
    {
        Assert.False(new InMemoryAgentCatalog().TryGet("missing", out _));
    }
}
