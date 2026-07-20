namespace FactoryOS.Shared.Extensions;

/// <summary>Convenience extensions applicable to any object.</summary>
public static class ObjectExtensions
{
    /// <summary>Wraps a single value in a read-only, single-element list.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value to wrap.</param>
    /// <returns>A single-element read-only list.</returns>
    public static IReadOnlyList<T> Yield<T>(this T value) => [value];

    /// <summary>Determines whether a value is equal to any of a set of candidates.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value to test.</param>
    /// <param name="candidates">The candidate values.</param>
    /// <returns><see langword="true"/> when the value equals one of the candidates.</returns>
    public static bool In<T>(this T value, params ReadOnlySpan<T> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (EqualityComparer<T>.Default.Equals(value, candidate))
            {
                return true;
            }
        }

        return false;
    }
}
