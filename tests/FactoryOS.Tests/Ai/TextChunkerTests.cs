using FactoryOS.Ai.Knowledge;

namespace FactoryOS.Tests.Ai;

public sealed class TextChunkerTests
{
    [Fact]
    public void Empty_or_whitespace_text_yields_no_chunks()
    {
        Assert.Empty(TextChunker.Chunk(""));
        Assert.Empty(TextChunker.Chunk("   \n\t "));
    }

    [Fact]
    public void Short_text_is_a_single_whitespace_normalized_chunk()
    {
        var chunks = TextChunker.Chunk("the  quick\n brown   fox");

        Assert.Single(chunks);
        Assert.Equal("the quick brown fox", chunks[0]);
    }

    [Fact]
    public void Long_text_splits_into_multiple_chunks_within_the_size_budget()
    {
        var text = string.Join(' ', Enumerable.Range(0, 200).Select(i => $"word{i}"));

        var chunks = TextChunker.Chunk(text, maxChars: 64, overlapChars: 16);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(c.Length <= 64, $"chunk exceeded budget: '{c}'"));
    }

    [Fact]
    public void Consecutive_chunks_overlap_and_never_split_a_word()
    {
        var text = string.Join(' ', Enumerable.Range(0, 60).Select(i => $"token{i}"));

        var chunks = TextChunker.Chunk(text, maxChars: 48, overlapChars: 16);

        Assert.True(chunks.Count >= 2);
        for (var i = 1; i < chunks.Count; i++)
        {
            var previousWords = chunks[i - 1].Split(' ');
            var firstOfNext = chunks[i].Split(' ')[0];
            Assert.Contains(firstOfNext, previousWords, StringComparer.Ordinal); // overlap carried a whole word
        }

        // No chunk contains a fragment like "toke" — every whitespace-split piece is a full "tokenN".
        Assert.All(chunks, c => Assert.All(c.Split(' '), w => Assert.StartsWith("token", w, StringComparison.Ordinal)));
    }
}
