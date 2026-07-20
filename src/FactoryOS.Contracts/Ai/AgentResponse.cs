namespace FactoryOS.Contracts.Ai;

/// <summary>An agent's result: its output text, the model that produced it and the knowledge it grounded on.</summary>
public sealed record AgentResponse
{
    /// <summary>The key of the agent that produced this response.</summary>
    public required string AgentKey { get; init; }

    /// <summary>The agent's output text.</summary>
    public required string Output { get; init; }

    /// <summary>The upstream chat model that produced the output.</summary>
    public required string Model { get; init; }

    /// <summary>The chunks the agent grounded on, in the order presented to the model; empty when ungrounded.</summary>
    public required IReadOnlyList<ScoredChunk> Grounding { get; init; }
}
