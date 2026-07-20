namespace FactoryOS.Domain.Results;

/// <summary>
/// Represents a domain or application error with a stable, machine-readable code, a human-readable
/// description and a classification.
/// </summary>
/// <param name="Code">A stable, machine-readable error code.</param>
/// <param name="Description">A human-readable description of the error.</param>
/// <param name="Type">The classification of the error.</param>
public sealed record Error(string Code, string Description, ErrorType Type)
{
    /// <summary>The sentinel representing the absence of an error (used by successful results).</summary>
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    /// <summary>Creates a generic <see cref="ErrorType.Failure"/> error.</summary>
    /// <param name="code">The machine-readable error code.</param>
    /// <param name="description">The human-readable description.</param>
    /// <returns>A new <see cref="Error"/>.</returns>
    public static Error Failure(string code, string description)
    {
        return new Error(code, description, ErrorType.Failure);
    }

    /// <summary>Creates a <see cref="ErrorType.Validation"/> error.</summary>
    /// <param name="code">The machine-readable error code.</param>
    /// <param name="description">The human-readable description.</param>
    /// <returns>A new <see cref="Error"/>.</returns>
    public static Error Validation(string code, string description)
    {
        return new Error(code, description, ErrorType.Validation);
    }

    /// <summary>Creates a <see cref="ErrorType.NotFound"/> error.</summary>
    /// <param name="code">The machine-readable error code.</param>
    /// <param name="description">The human-readable description.</param>
    /// <returns>A new <see cref="Error"/>.</returns>
    public static Error NotFound(string code, string description)
    {
        return new Error(code, description, ErrorType.NotFound);
    }

    /// <summary>Creates a <see cref="ErrorType.Conflict"/> error.</summary>
    /// <param name="code">The machine-readable error code.</param>
    /// <param name="description">The human-readable description.</param>
    /// <returns>A new <see cref="Error"/>.</returns>
    public static Error Conflict(string code, string description)
    {
        return new Error(code, description, ErrorType.Conflict);
    }
}
