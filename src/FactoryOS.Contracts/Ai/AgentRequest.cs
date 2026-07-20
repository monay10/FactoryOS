namespace FactoryOS.Contracts.Ai;

/// <summary>A single invocation of an agent: which agent, on whose behalf, with what input and variables.</summary>
public sealed record AgentRequest
{
    /// <summary>The tenant the agent runs for; scopes grounding and generation.</summary>
    public required string Tenant { get; init; }

    /// <summary>The key of the agent to run.</summary>
    public required string AgentKey { get; init; }

    /// <summary>The task or trigger text the agent should act on.</summary>
    public required string Input { get; init; }

    /// <summary>Optional variables bound into the agent's system-prompt placeholders.</summary>
    public IReadOnlyDictionary<string, string>? Variables { get; init; }
}
