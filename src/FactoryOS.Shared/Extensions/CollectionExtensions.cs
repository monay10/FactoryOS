namespace FactoryOS.Shared.Extensions;

/// <summary>Convenience extensions for mutable <see cref="ICollection{T}"/> instances.</summary>
public static class CollectionExtensions
{
    /// <summary>Determines whether a collection is <see langword="null"/> or empty.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="collection">The collection to test.</param>
    /// <returns><see langword="true"/> when the collection is null or has no items.</returns>
    public static bool IsNullOrEmpty<T>(this ICollection<T>? collection) => collection is null || collection.Count == 0;

    /// <summary>Adds a sequence of items to a collection.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="collection">The collection to add to.</param>
    /// <param name="items">The items to add.</param>
    public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(items);
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }
}
