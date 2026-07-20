namespace FactoryOS.Domain.Results;

/// <summary>Classifies the nature of an <see cref="Error"/> so callers can react appropriately.</summary>
public enum ErrorType
{
    /// <summary>An unexpected or otherwise unclassified failure.</summary>
    Failure = 0,

    /// <summary>A validation failure caused by invalid input.</summary>
    Validation = 1,

    /// <summary>A requested resource could not be found.</summary>
    NotFound = 2,

    /// <summary>The operation conflicts with the current state of a resource.</summary>
    Conflict = 3,
}
