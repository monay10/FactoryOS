namespace FactoryOS.Contracts.Ai;

/// <summary>The Company Brain's grounded answer, with the sources it was built from.</summary>
public sealed record BrainAnswer
{
    /// <summary>The generated answer text.</summary>
    public required string Answer { get; init; }

    /// <summary>The knowledge chunks that grounded the answer, in the order presented to the model.</summary>
    public required IReadOnlyList<BrainCitation> Citations { get; init; }

    /// <summary>The upstream chat model that produced the answer.</summary>
    public required string Model { get; init; }
}

/// <summary>A single source the Company Brain grounded its answer on.</summary>
public sealed record BrainCitation
{
    /// <summary>The source document identifier.</summary>
    public required string Source { get; init; }

    /// <summary>The grounding chunk's identifier.</summary>
    public required string ChunkId { get; init; }

    /// <summary>The chunk's similarity to the question, in <c>[-1, 1]</c>.</summary>
    public required double Score { get; init; }
}
