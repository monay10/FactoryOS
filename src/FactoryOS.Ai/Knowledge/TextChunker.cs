using System.Text;

namespace FactoryOS.Ai.Knowledge;

/// <summary>
/// Splits a document's text into overlapping, word-aligned chunks suitable for embedding. Overlap preserves
/// context across chunk boundaries so a passage split in two still retrieves well. Pure and deterministic —
/// no I/O — so it is fully offline-testable.
/// </summary>
public static class TextChunker
{
    /// <summary>The default target chunk size, in characters.</summary>
    public const int DefaultMaxChars = 512;

    /// <summary>The default overlap between consecutive chunks, in characters.</summary>
    public const int DefaultOverlapChars = 64;

    /// <summary>Splits <paramref name="text"/> into overlapping chunks, never breaking inside a word.</summary>
    /// <param name="text">The text to split.</param>
    /// <param name="maxChars">The target maximum chunk length in characters. Must be positive.</param>
    /// <param name="overlapChars">How many characters of the previous chunk to repeat at the start of the next. Must be non-negative and less than <paramref name="maxChars"/>.</param>
    /// <returns>The chunks, in document order; empty when the text is null or whitespace.</returns>
    public static IReadOnlyList<string> Chunk(
        string text,
        int maxChars = DefaultMaxChars,
        int overlapChars = DefaultOverlapChars)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxChars);
        ArgumentOutOfRangeException.ThrowIfNegative(overlapChars);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(overlapChars, maxChars);

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();
        var current = new StringBuilder();

        foreach (var word in words)
        {
            if (current.Length > 0 && current.Length + 1 + word.Length > maxChars)
            {
                chunks.Add(current.ToString());
                var tail = OverlapTail(current, overlapChars);
                current.Clear();
                current.Append(tail);
            }

            if (current.Length > 0)
            {
                current.Append(' ');
            }

            current.Append(word);
        }

        if (current.Length > 0)
        {
            chunks.Add(current.ToString());
        }

        return chunks;
    }

    private static string OverlapTail(StringBuilder chunk, int overlapChars)
    {
        if (overlapChars == 0)
        {
            return string.Empty;
        }

        var text = chunk.ToString();
        if (text.Length <= overlapChars)
        {
            return text;
        }

        // Start the tail at a word boundary so we never repeat a partial word.
        var start = text.Length - overlapChars;
        var boundary = text.IndexOf(' ', start);
        return boundary < 0 ? string.Empty : text[(boundary + 1)..];
    }
}
