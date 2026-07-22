using FactoryOS.Connectors.Runtime.Domain;

namespace FactoryOS.Connectors.Runtime.Execution;

/// <summary>
/// Waits between retry attempts. It is a seam rather than a bare <see cref="Task.Delay(TimeSpan)"/> so a test
/// can prove the <b>backoff schedule</b> without spending it: a retry policy whose only proof is a slow test
/// is a policy nobody will keep testing.
/// </summary>
public interface IConnectorDelay
{
    /// <summary>Waits for a period.</summary>
    /// <param name="delay">How long to wait.</param>
    /// <param name="cancellationToken">A token to cut the wait short.</param>
    /// <returns>A task that completes when the wait is over.</returns>
    Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken);
}

/// <summary>The default <see cref="IConnectorDelay"/>, which actually waits.</summary>
public sealed class TaskConnectorDelay : IConnectorDelay
{
    /// <inheritdoc />
    public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        delay <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(delay, cancellationToken);
}

/// <summary>
/// Decides whether a failed attempt is tried again, and how long to wait first.
/// <para>
/// Two conditions must both hold, and they are separate on purpose. The <b>error</b> must be one a later
/// attempt could survive — retrying a malformed request is spending three calls to be told the same thing
/// three times. And the <b>operation</b> must be idempotent — retrying a non-idempotent write is how one
/// purchase order becomes three, and no amount of backoff makes that safe.
/// </para>
/// </summary>
public sealed class RetryEngine
{
    private readonly IConnectorDelay _delay;

    /// <summary>Initializes a new instance of the <see cref="RetryEngine"/> class.</summary>
    /// <param name="delay">How the engine waits between attempts.</param>
    public RetryEngine(IConnectorDelay delay)
    {
        ArgumentNullException.ThrowIfNull(delay);
        _delay = delay;
    }

    /// <summary>Determines whether another attempt should be made.</summary>
    /// <param name="policy">The retry policy.</param>
    /// <param name="operation">The operation being invoked.</param>
    /// <param name="error">Why the last attempt failed.</param>
    /// <param name="attemptsMade">How many attempts have already been made.</param>
    /// <returns><see langword="true"/> when another attempt is warranted.</returns>
    public bool ShouldRetry(
        ConnectorRetryPolicy policy, ConnectorOperation operation, ConnectorError error, int attemptsMade)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(error);

        return attemptsMade < policy.MaxAttempts && operation.Idempotent && error.IsRetryable;
    }

    /// <summary>Computes the delay before an attempt.</summary>
    /// <param name="policy">The retry policy.</param>
    /// <param name="attempt">The attempt about to be made, counting the first as one.</param>
    /// <returns>How long to wait first.</returns>
    public TimeSpan DelayBefore(ConnectorRetryPolicy policy, int attempt)
    {
        ArgumentNullException.ThrowIfNull(policy);
        return policy.DelayBefore(attempt);
    }

    /// <summary>Waits before an attempt.</summary>
    /// <param name="policy">The retry policy.</param>
    /// <param name="attempt">The attempt about to be made.</param>
    /// <param name="cancellationToken">A token to cut the wait short.</param>
    /// <returns>A task that completes when the wait is over.</returns>
    public Task WaitBeforeAsync(ConnectorRetryPolicy policy, int attempt, CancellationToken cancellationToken) =>
        _delay.WaitAsync(DelayBefore(policy, attempt), cancellationToken);
}
