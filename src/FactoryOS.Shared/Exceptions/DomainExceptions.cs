namespace FactoryOS.Shared.Exceptions;

/// <summary>
/// Base class for exceptions that represent the violation of a domain or application rule. Each exception carries a
/// stable, machine-readable <see cref="Code"/> so callers (for example an API problem-details mapper) can translate it
/// without string-matching the message.
/// </summary>
public abstract class DomainException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="DomainException"/> class.</summary>
    protected DomainException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DomainException"/> class with a message.</summary>
    /// <param name="message">The message that describes the error.</param>
    protected DomainException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DomainException"/> class with a message and inner exception.</summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    protected DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Gets the stable, machine-readable code that classifies this error.</summary>
    public abstract string Code { get; }
}

/// <summary>Raised when a business rule or invariant is violated.</summary>
public sealed class BusinessException : DomainException
{
    /// <summary>Initializes a new instance of the <see cref="BusinessException"/> class.</summary>
    public BusinessException()
        : base("A business rule was violated.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BusinessException"/> class with a message.</summary>
    /// <param name="message">The message that describes the violated rule.</param>
    public BusinessException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BusinessException"/> class with a message and inner exception.</summary>
    /// <param name="message">The message that describes the violated rule.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public BusinessException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <inheritdoc />
    public override string Code => "business_rule_violated";
}

/// <summary>Raised when one or more input validation rules fail.</summary>
public sealed class ValidationException : DomainException
{
    /// <summary>Initializes a new instance of the <see cref="ValidationException"/> class.</summary>
    public ValidationException()
        : this(["One or more validation errors occurred."])
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ValidationException"/> class with a message.</summary>
    /// <param name="message">The message that describes the validation failure.</param>
    public ValidationException(string message)
        : base(message)
    {
        Errors = [message];
    }

    /// <summary>Initializes a new instance of the <see cref="ValidationException"/> class with a message and inner exception.</summary>
    /// <param name="message">The message that describes the validation failure.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Errors = [message];
    }

    /// <summary>Initializes a new instance of the <see cref="ValidationException"/> class with a set of messages.</summary>
    /// <param name="errors">The individual validation error messages.</param>
    public ValidationException(IEnumerable<string> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors is null ? [] : [.. errors];
    }

    /// <summary>Gets the individual validation error messages.</summary>
    public IReadOnlyList<string> Errors { get; }

    /// <inheritdoc />
    public override string Code => "validation_failed";
}

/// <summary>Raised when a requested resource does not exist.</summary>
public sealed class NotFoundException : DomainException
{
    /// <summary>Initializes a new instance of the <see cref="NotFoundException"/> class.</summary>
    public NotFoundException()
        : base("The requested resource was not found.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NotFoundException"/> class with a message.</summary>
    /// <param name="message">The message that describes what was not found.</param>
    public NotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="NotFoundException"/> class with a message and inner exception.</summary>
    /// <param name="message">The message that describes what was not found.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public NotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates a <see cref="NotFoundException"/> for a named entity and key.</summary>
    /// <param name="entity">The entity type that was searched.</param>
    /// <param name="key">The key that was not found.</param>
    /// <returns>A new <see cref="NotFoundException"/>.</returns>
    public static NotFoundException For(string entity, object key)
    {
        return new NotFoundException($"{entity} '{key}' was not found.");
    }

    /// <inheritdoc />
    public override string Code => "not_found";
}

/// <summary>Raised when an operation conflicts with the current state of a resource.</summary>
public sealed class ConflictException : DomainException
{
    /// <summary>Initializes a new instance of the <see cref="ConflictException"/> class.</summary>
    public ConflictException()
        : base("The operation conflicts with the current state.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ConflictException"/> class with a message.</summary>
    /// <param name="message">The message that describes the conflict.</param>
    public ConflictException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ConflictException"/> class with a message and inner exception.</summary>
    /// <param name="message">The message that describes the conflict.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <inheritdoc />
    public override string Code => "conflict";
}

/// <summary>Raised when a request is not authenticated.</summary>
public sealed class UnauthorizedException : DomainException
{
    /// <summary>Initializes a new instance of the <see cref="UnauthorizedException"/> class.</summary>
    public UnauthorizedException()
        : base("Authentication is required.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="UnauthorizedException"/> class with a message.</summary>
    /// <param name="message">The message that describes the failure.</param>
    public UnauthorizedException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="UnauthorizedException"/> class with a message and inner exception.</summary>
    /// <param name="message">The message that describes the failure.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public UnauthorizedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <inheritdoc />
    public override string Code => "unauthorized";
}

/// <summary>Raised when an authenticated caller lacks permission for an operation.</summary>
public sealed class ForbiddenException : DomainException
{
    /// <summary>Initializes a new instance of the <see cref="ForbiddenException"/> class.</summary>
    public ForbiddenException()
        : base("You do not have permission to perform this action.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ForbiddenException"/> class with a message.</summary>
    /// <param name="message">The message that describes the failure.</param>
    public ForbiddenException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ForbiddenException"/> class with a message and inner exception.</summary>
    /// <param name="message">The message that describes the failure.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ForbiddenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <inheritdoc />
    public override string Code => "forbidden";
}
