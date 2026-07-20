using FactoryOS.Contracts.Events;

namespace FactoryOS.EventBus.InProcess;

/// <summary>Thread-safe, in-memory <see cref="IEventBusMetrics"/> exposing cumulative counters.</summary>
public sealed class InMemoryEventBusMetrics : IEventBusMetrics
{
    private long _published;
    private long _handled;
    private long _retried;
    private long _deadLettered;

    /// <summary>Gets the total number of events published.</summary>
    public long Published => Interlocked.Read(ref _published);

    /// <summary>Gets the total number of events successfully handled.</summary>
    public long Handled => Interlocked.Read(ref _handled);

    /// <summary>Gets the total number of retries scheduled.</summary>
    public long Retried => Interlocked.Read(ref _retried);

    /// <summary>Gets the total number of messages dead-lettered.</summary>
    public long DeadLettered => Interlocked.Read(ref _deadLettered);

    /// <inheritdoc />
    public void RecordPublished(string eventType, EventPriority priority)
    {
        Interlocked.Increment(ref _published);
    }

    /// <inheritdoc />
    public void RecordHandled(string eventType)
    {
        Interlocked.Increment(ref _handled);
    }

    /// <inheritdoc />
    public void RecordRetry(string eventType)
    {
        Interlocked.Increment(ref _retried);
    }

    /// <inheritdoc />
    public void RecordDeadLettered(string eventType)
    {
        Interlocked.Increment(ref _deadLettered);
    }
}
