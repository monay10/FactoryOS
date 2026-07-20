namespace FactoryOS.Contracts.Connectors;

/// <summary>
/// The declarative mapping from a source system's dialect into the Standard Model (its
/// <c>mapping.json</c>). One manifest describes every source entity a connector can normalize.
/// </summary>
public sealed record MappingManifest
{
    /// <summary>Gets the source system these mappings apply to (for example <c>Logo</c>).</summary>
    public required string SourceSystem { get; init; }

    /// <summary>Gets the per-source-entity mapping rules.</summary>
    public required IReadOnlyList<EntityMapping> Entities { get; init; }

    /// <summary>Finds the mapping for a source entity, or <see langword="null"/> when none is declared.</summary>
    /// <param name="sourceEntity">The source-side entity name.</param>
    /// <returns>The matching <see cref="EntityMapping"/>, or <see langword="null"/>.</returns>
    public EntityMapping? FindEntity(string sourceEntity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEntity);

        foreach (var entity in Entities)
        {
            if (string.Equals(entity.SourceEntity, sourceEntity, StringComparison.OrdinalIgnoreCase))
            {
                return entity;
            }
        }

        return null;
    }
}
