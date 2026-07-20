namespace FactoryOS.Domain.Results;

/// <summary>Represents the outcome of an operation that either succeeds or fails with an <see cref="Error"/>.</summary>
public class Result
{
    /// <summary>Initializes a new instance of the <see cref="Result"/> class.</summary>
    /// <param name="isSuccess">Whether the operation succeeded.</param>
    /// <param name="error">The associated error, or <see cref="Error.None"/> when successful.</param>
    /// <exception cref="InvalidOperationException">Thrown when success and error state are inconsistent.</exception>
    protected internal Result(bool isSuccess, Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException("A successful result cannot carry an error.");
        }

        if (!isSuccess && error == Error.None)
        {
            throw new InvalidOperationException("A failure result must carry an error.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Gets a value indicating whether the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>Gets the error associated with a failure, or <see cref="Error.None"/> on success.</summary>
    public Error Error { get; }

    /// <summary>Creates a successful result.</summary>
    /// <returns>A successful <see cref="Result"/>.</returns>
    public static Result Success()
    {
        return new Result(true, Error.None);
    }

    /// <summary>Creates a successful result carrying a value.</summary>
    /// <typeparam name="TValue">The type of the carried value.</typeparam>
    /// <param name="value">The value produced by the operation.</param>
    /// <returns>A successful <see cref="Result{TValue}"/>.</returns>
    public static Result<TValue> Success<TValue>(TValue value)
    {
        return new Result<TValue>(value, true, Error.None);
    }

    /// <summary>Creates a failure result.</summary>
    /// <param name="error">The error describing the failure.</param>
    /// <returns>A failed <see cref="Result"/>.</returns>
    public static Result Failure(Error error)
    {
        return new Result(false, error);
    }

    /// <summary>Creates a failure result for an operation that would have produced a value.</summary>
    /// <typeparam name="TValue">The type of the value that was not produced.</typeparam>
    /// <param name="error">The error describing the failure.</param>
    /// <returns>A failed <see cref="Result{TValue}"/>.</returns>
    public static Result<TValue> Failure<TValue>(Error error)
    {
        return new Result<TValue>(default, false, error);
    }
}

/// <summary>Represents the outcome of an operation that yields a value of type <typeparamref name="TValue"/>.</summary>
/// <typeparam name="TValue">The type of the produced value.</typeparam>
public class Result<TValue> : Result
{
    private readonly TValue? _value;

    /// <summary>Initializes a new instance of the <see cref="Result{TValue}"/> class.</summary>
    /// <param name="value">The produced value, or <see langword="default"/> on failure.</param>
    /// <param name="isSuccess">Whether the operation succeeded.</param>
    /// <param name="error">The associated error, or <see cref="Error.None"/> when successful.</param>
    protected internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>Gets the value of a successful result.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the result is a failure.</exception>
    public TValue Value
    {
        get
        {
            if (IsFailure)
            {
                throw new InvalidOperationException("The value of a failure result cannot be accessed.");
            }

            return _value!;
        }
    }

    /// <summary>Implicitly wraps a value in a successful result.</summary>
    /// <param name="value">The value to wrap.</param>
    public static implicit operator Result<TValue>(TValue value)
    {
        return Success(value);
    }
}
