using FactoryOS.Domain.Results;

namespace FactoryOS.Connectors.Transforms;

/// <summary>
/// Applies named value transforms referenced by field mappings. Transforms convert a raw source value
/// into its canonical Standard Model form (for example parsing a decimal or upper-casing a code).
/// </summary>
public interface IValueTransformer
{
    /// <summary>Applies the named transform to a value.</summary>
    /// <param name="name">The transform name, or <see langword="null"/>/empty for the identity transform.</param>
    /// <param name="value">The value to transform.</param>
    /// <returns>A successful result with the transformed value, or a failure when the transform is unknown or fails.</returns>
    Result<object?> Apply(string? name, object? value);

    /// <summary>Determines whether a transform with the given name is registered.</summary>
    /// <param name="name">The transform name.</param>
    /// <returns><see langword="true"/> when the transform exists; otherwise <see langword="false"/>.</returns>
    bool Supports(string name);
}
