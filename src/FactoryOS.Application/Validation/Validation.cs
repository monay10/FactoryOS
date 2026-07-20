using FactoryOS.Application.Messaging;

namespace FactoryOS.Application.Validation;

/// <summary>A single validation failure: which member failed and why.</summary>
/// <param name="PropertyName">The name of the member that failed validation.</param>
/// <param name="Message">The human-readable failure message.</param>
public sealed record ValidationFailure(string PropertyName, string Message);

/// <summary>The outcome of validating an instance.</summary>
public interface IValidationResult
{
    /// <summary>Gets a value indicating whether validation succeeded (no failures).</summary>
    bool IsValid { get; }

    /// <summary>Gets the validation failures (empty when valid).</summary>
    IReadOnlyList<ValidationFailure> Failures { get; }
}

/// <summary>The default <see cref="IValidationResult"/>, with success/failure factories.</summary>
public sealed class ValidationResult : IValidationResult
{
    private static readonly ValidationResult SuccessInstance = new([]);

    private ValidationResult(IReadOnlyList<ValidationFailure> failures) => Failures = failures;

    /// <inheritdoc />
    public bool IsValid => Failures.Count == 0;

    /// <inheritdoc />
    public IReadOnlyList<ValidationFailure> Failures { get; }

    /// <summary>Gets a shared, successful result.</summary>
    /// <returns>A valid result with no failures.</returns>
    public static ValidationResult Success() => SuccessInstance;

    /// <summary>Creates a failed result from a set of failures.</summary>
    /// <param name="failures">The validation failures.</param>
    /// <returns>An invalid result.</returns>
    public static ValidationResult Failure(IEnumerable<ValidationFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        return new ValidationResult([.. failures]);
    }
}

/// <summary>The instance under validation together with the ambient request context.</summary>
/// <typeparam name="T">The type being validated.</typeparam>
public interface IValidationContext<out T>
{
    /// <summary>Gets the instance being validated.</summary>
    T Instance { get; }

    /// <summary>Gets the ambient request context, when validation runs inside a request.</summary>
    IRequestContext? Request { get; }
}

/// <summary>Validates instances of a type, returning an <see cref="IValidationResult"/> rather than throwing.</summary>
/// <typeparam name="T">The type to validate.</typeparam>
public interface IValidator<in T>
{
    /// <summary>Validates an instance.</summary>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The validation result.</returns>
    Task<IValidationResult> ValidateAsync(T instance, CancellationToken cancellationToken = default);
}

/// <summary>A validator specialized for a command.</summary>
/// <typeparam name="TCommand">The command type.</typeparam>
public interface ICommandValidator<in TCommand> : IValidator<TCommand>
    where TCommand : ICommand
{
}

/// <summary>A validator specialized for a query.</summary>
/// <typeparam name="TQuery">The query type.</typeparam>
/// <typeparam name="TResult">The query's result type.</typeparam>
public interface IQueryValidator<in TQuery, TResult> : IValidator<TQuery>
    where TQuery : IQuery<TResult>
{
}
