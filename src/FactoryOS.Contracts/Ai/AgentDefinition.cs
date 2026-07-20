namespace FactoryOS.Contracts.Ai;

/// <summary>
/// The manifest of an AI agent — a digital worker. Per the Constitution, all agents share one runtime and
/// differ only by their manifest and prompt: this record <b>is</b> that difference. It carries no code, only
/// data (a system prompt, a logical chat model and optional retrieval grounding).
/// </summary>
public sealed record AgentDefinition
{
    /// <summary>The unique agent key the runtime resolves on (for example <c>maintenance.triage</c>).</summary>
    public required string Key { get; init; }

    /// <summary>A human-readable name.</summary>
    public required string Name { get; init; }

    /// <summary>The manifest version; the catalog keeps the highest.</summary>
    public int Version { get; init; } = 1;

    /// <summary>An optional description of what the agent does.</summary>
    public string? Description { get; init; }

    /// <summary>The agent's system prompt; may contain <c>{{variable}}</c> placeholders bound at run time.</summary>
    public required string SystemPrompt { get; init; }

    /// <summary>The logical chat model key the agent reasons with.</summary>
    public required string ChatModel { get; init; }

    /// <summary>Optional knowledge-base grounding; when set, the agent retrieves context before answering.</summary>
    public AgentGrounding? Grounding { get; init; }
}

/// <summary>Retrieval-grounding settings for an agent.</summary>
public sealed record AgentGrounding
{
    /// <summary>The logical embedding model key used to retrieve grounding.</summary>
    public required string EmbeddingModel { get; init; }

    /// <summary>How many knowledge chunks to ground on.</summary>
    public int TopK { get; init; } = 4;
}
