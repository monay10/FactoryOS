namespace FactoryOS.Application.Mapping;

/// <summary>Maps objects from one shape to another (for example an entity to a read model or DTO).</summary>
public interface IObjectMapper
{
    /// <summary>Maps a source object to a destination type.</summary>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <param name="source">The source object.</param>
    /// <returns>The mapped destination instance.</returns>
    TDestination Map<TDestination>(object source);

    /// <summary>Maps a source object to a destination type with both types known.</summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <param name="source">The source object.</param>
    /// <returns>The mapped destination instance.</returns>
    TDestination Map<TSource, TDestination>(TSource source);
}
