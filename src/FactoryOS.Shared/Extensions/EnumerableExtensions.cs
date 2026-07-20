namespace FactoryOS.Shared.Extensions;

/// <summary>Convenience extensions for <see cref="IEnumerable{T}"/>.</summary>
public static class EnumerableExtensions
{
    /// <summary>Determines whether a sequence is <see langword="null"/> or contains no elements.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The sequence to test.</param>
    /// <returns><see langword="true"/> when the sequence is null or empty.</returns>
    public static bool IsNullOrEmpty<T>(this IEnumerable<T>? source) => source is null || !source.Any();

    /// <summary>Filters out <see langword="null"/> references from a sequence.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The sequence to filter.</param>
    /// <returns>The non-null elements.</returns>
    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Where(item => item is not null)!;
    }

    /// <summary>Materializes a sequence as a read-only list, avoiding a copy when it already is one.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The sequence.</param>
    /// <returns>A read-only list of the elements.</returns>
    public static IReadOnlyList<T> AsReadOnlyList<T>(this IEnumerable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source as IReadOnlyList<T> ?? source.ToArray();
    }
}
