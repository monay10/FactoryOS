namespace FactoryOS.Contracts.Events;

/// <summary>Stores messages that could not be handled after exhausting all retries.</summary>
public interface IDeadLetterQueue
{
    /// <summary>Adds a dead-lettered message to the queue.</summary>
    /// <param name="envelope">The dead-letter envelope to store.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the message has been stored.</returns>
    Task EnqueueAsync(DeadLetterEnvelope envelope, CancellationToken cancellationToken = default);
}
