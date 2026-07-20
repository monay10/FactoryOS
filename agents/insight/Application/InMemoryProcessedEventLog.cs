using System.Collections.Concurrent;

namespace FactoryOS.Agents.Insight.Application;

/// <summary>The default in-memory <see cref="IProcessedEventLog"/>, backed by a concurrent set of event ids.</summary>
public sealed class InMemoryProcessedEventLog : IProcessedEventLog
{
    private readonly ConcurrentDictionary<Guid, byte> _seen = new();

    /// <inheritdoc />
    public bool TryMarkProcessed(Guid eventId) => _seen.TryAdd(eventId, 0);
}
