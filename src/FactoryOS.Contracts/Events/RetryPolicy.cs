namespace FactoryOS.Contracts.Events;

/// <summary>Defines how many times a failing handler is retried and the exponential back-off between attempts.</summary>
public sealed class RetryPolicy
{
    /// <summary>Initializes a new instance of the <see cref="RetryPolicy"/> class.</summary>
    /// <param name="maxAttempts">The maximum number of delivery attempts (at least one).</param>
    /// <param name="baseDelay">The base delay used for exponential back-off between attempts.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the arguments are out of range.</exception>
    public RetryPolicy(int maxAttempts, TimeSpan baseDelay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(baseDelay, TimeSpan.Zero);

        MaxAttempts = maxAttempts;
        BaseDelay = baseDelay;
    }

    /// <summary>Gets the maximum number of delivery attempts.</summary>
    public int MaxAttempts { get; }

    /// <summary>Gets the base delay used for exponential back-off.</summary>
    public TimeSpan BaseDelay { get; }

    /// <summary>Computes the delay to wait before the attempt that follows the given one.</summary>
    /// <param name="attempt">The 1-based attempt that has just failed.</param>
    /// <returns>The back-off delay before the next attempt.</returns>
    public TimeSpan GetDelay(int attempt)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attempt, 1);

        var multiplier = Math.Pow(2, attempt - 1);
        return TimeSpan.FromMilliseconds(BaseDelay.TotalMilliseconds * multiplier);
    }
}
