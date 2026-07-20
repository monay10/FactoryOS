namespace FactoryOS.Domain.Abstractions;

/// <summary>Abstraction over the system clock, keeping the domain deterministic and testable.</summary>
public interface IDateTimeProvider
{
    /// <summary>Gets the current UTC date and time.</summary>
    DateTimeOffset UtcNow { get; }
}
