namespace FactoryOS.Domain.Abstractions;

/// <summary>Abstraction for generating unique, time-ordered identifiers for new aggregates.</summary>
public interface IIdGenerator
{
    /// <summary>Creates a new unique, time-ordered identifier.</summary>
    /// <returns>A new <see cref="Guid"/> value.</returns>
    Guid NewId();
}
