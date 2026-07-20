using System.Collections.Concurrent;
using FactoryOS.Contracts.Events;

namespace FactoryOS.EventBus.InProcess;

/// <summary>
/// In-memory <see cref="IDeadLetterQueue"/> that retains dead-lettered messages for inspection,
/// alerting and manual replay. Intended as the default until a durable transport is configured.
/// </summary>
public sealed class InMemoryDeadLetterQueue : IDeadLetterQueue
{
    private readonly ConcurrentQueue<DeadLetterEnvelope> _messages = new();

    /// <summary>Gets a point-in-time snapshot of the currently stored dead-lettered messages.</summary>
    public IReadOnlyCollection<DeadLetterEnvelope> Messages => _messages.ToArray();

    /// <inheritdoc />
    public Task EnqueueAsync(DeadLetterEnvelope envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        _messages.Enqueue(envelope);
        return Task.CompletedTask;
    }
}
