using System.Text;
using FactoryOS.Contracts.Ai;

namespace FactoryOS.Ai.Knowledge;

/// <summary>
/// Formats retrieved chunks into a grounding context block that can be injected into a prompt (for example as a
/// <c>{{context}}</c> variable for the Prompt Engine). This is the bridge from retrieval to augmented generation.
/// </summary>
public static class RagContext
{
    /// <summary>
    /// Renders chunks as a numbered, source-attributed context block, in the order given (highest-ranked first).
    /// Returns an empty string when there are no chunks so callers can detect "no grounding available".
    /// </summary>
    /// <param name="chunks">The retrieved chunks.</param>
    /// <returns>A newline-delimited context block, or an empty string when <paramref name="chunks"/> is empty.</returns>
    public static string Build(IReadOnlyList<ScoredChunk> chunks)
    {
        ArgumentNullException.ThrowIfNull(chunks);

        if (chunks.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i].Chunk;
            builder.Append('[').Append(i + 1).Append("] (").Append(chunk.Source).Append(") ").Append(chunk.Text);
            if (i < chunks.Count - 1)
            {
                builder.Append('\n');
            }
        }

        return builder.ToString();
    }
}
