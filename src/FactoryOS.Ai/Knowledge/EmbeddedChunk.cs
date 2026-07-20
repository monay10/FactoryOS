using FactoryOS.Contracts.Ai;

namespace FactoryOS.Ai.Knowledge;

/// <summary>A knowledge chunk paired with its embedding vector, as held by an <see cref="IKnowledgeStore"/>.</summary>
/// <param name="Chunk">The chunk metadata and text.</param>
/// <param name="Vector">The embedding of <see cref="KnowledgeChunk.Text"/>.</param>
public sealed record EmbeddedChunk(KnowledgeChunk Chunk, IReadOnlyList<float> Vector);
