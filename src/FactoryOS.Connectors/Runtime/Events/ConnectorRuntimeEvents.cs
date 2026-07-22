using System.Collections.Concurrent;
using FactoryOS.Connectors.Framework.Health;
using FactoryOS.Connectors.Runtime.Domain;

namespace FactoryOS.Connectors.Runtime.Events;

/// <summary>The common shape of everything the connector runtime announces.</summary>
public abstract record ConnectorRuntimeEvent
{
    /// <summary>Gets the tenant the event belongs to, or <see langword="null"/> for platform-wide events.</summary>
    public string? Tenant { get; init; }

    /// <summary>Gets when the event occurred.</summary>
    public DateTimeOffset OccurredUtc { get; init; }

    /// <summary>Gets the identifiers tying the event to the work that caused it.</summary>
    public ConnectorCorrelation Correlation { get; init; } = ConnectorCorrelation.Empty;
}

/// <summary>A connector definition entered the runtime's catalogue.</summary>
/// <param name="Definition">The definition key.</param>
/// <param name="Version">The version registered.</param>
/// <param name="Category">The kind of external system.</param>
public sealed record ConnectorRegistered(string Definition, string Version, ConnectorCategory Category)
    : ConnectorRuntimeEvent;

/// <summary>A connector definition was loaded and its operation handler attached.</summary>
/// <param name="Definition">The definition key.</param>
/// <param name="Operations">How many operations it offers.</param>
public sealed record ConnectorLoaded(string Definition, int Operations) : ConnectorRuntimeEvent;

/// <summary>A tenant's connector instance started and will accept invocations.</summary>
/// <param name="Instance">The instance key.</param>
/// <param name="Definition">The definition it activates.</param>
public sealed record ConnectorStarted(string Instance, string Definition) : ConnectorRuntimeEvent;

/// <summary>A tenant's connector instance stopped.</summary>
/// <param name="Instance">The instance key.</param>
/// <param name="Reason">Why it stopped.</param>
public sealed record ConnectorStopped(string Instance, string Reason) : ConnectorRuntimeEvent;

/// <summary>An operation was invoked and finished.</summary>
/// <param name="Telemetry">What the invocation cost and how it ended.</param>
public sealed record ConnectorInvoked(ConnectorTelemetry Telemetry) : ConnectorRuntimeEvent;

/// <summary>An invocation failed.</summary>
/// <param name="Instance">The instance key.</param>
/// <param name="Operation">The operation name.</param>
/// <param name="Error">Why it failed.</param>
/// <param name="Attempts">How many attempts were made.</param>
public sealed record ConnectorFailed(string Instance, string Operation, ConnectorError Error, int Attempts)
    : ConnectorRuntimeEvent;

/// <summary>An instance that had been failing succeeded again.</summary>
/// <param name="Instance">The instance key.</param>
/// <param name="Operation">The operation that succeeded.</param>
public sealed record ConnectorRecovered(string Instance, string Operation) : ConnectorRuntimeEvent;

/// <summary>An instance's health verdict changed.</summary>
/// <param name="Instance">The instance key.</param>
/// <param name="Previous">The status it held before.</param>
/// <param name="Current">The status it holds now.</param>
/// <param name="Detail">Why it changed.</param>
public sealed record ConnectorHealthChanged(
    string Instance, ConnectorHealthStatus Previous, ConnectorHealthStatus Current, string Detail)
    : ConnectorRuntimeEvent;

/// <summary>An instance's endpoint, credential reference or settings changed.</summary>
/// <param name="Instance">The instance key.</param>
/// <param name="ChangedBy">Who changed it.</param>
public sealed record ConnectorConfigurationChanged(string Instance, string ChangedBy) : ConnectorRuntimeEvent;

/// <summary>
/// Receives what the connector runtime announces.
/// <para>
/// Every registered sink is notified. Unlike the single-consumer seams older engines grew, this one fans out
/// from the start: observability, an event-bus forwarder and a SIEM exporter are three subscribers to the
/// same stream, not three layers of wrapping around one.
/// </para>
/// </summary>
public interface IConnectorRuntimeEventSink
{
    /// <summary>Receives an event.</summary>
    /// <param name="runtimeEvent">The event.</param>
    void Publish(ConnectorRuntimeEvent runtimeEvent);
}

/// <summary>
/// An in-memory <see cref="IConnectorRuntimeEventSink"/> that keeps a bounded, ordered history — the default
/// so a container is useful without a broker, and bounded so a busy factory cannot fill memory with its own
/// event log.
/// </summary>
public sealed class InMemoryConnectorRuntimeEventSink : IConnectorRuntimeEventSink
{
    /// <summary>How many events are retained before the oldest are dropped.</summary>
    public const int HistoryLimit = 1000;

    private readonly ConcurrentQueue<ConnectorRuntimeEvent> _events = new();

    /// <inheritdoc />
    public void Publish(ConnectorRuntimeEvent runtimeEvent)
    {
        ArgumentNullException.ThrowIfNull(runtimeEvent);

        _events.Enqueue(runtimeEvent);
        while (_events.Count > HistoryLimit && _events.TryDequeue(out _))
        {
            // Drop the oldest until the history is back inside its bound.
        }
    }

    /// <summary>Gets every retained event, oldest first.</summary>
    /// <returns>The events.</returns>
    public IReadOnlyList<ConnectorRuntimeEvent> All() => [.. _events];

    /// <summary>Gets the retained events of one type, oldest first.</summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <returns>The matching events.</returns>
    public IReadOnlyList<TEvent> OfType<TEvent>()
        where TEvent : ConnectorRuntimeEvent => [.. _events.OfType<TEvent>()];

    /// <summary>Forgets every retained event.</summary>
    public void Clear()
    {
        while (_events.TryDequeue(out _))
        {
            // Drain.
        }
    }
}

/// <summary>Publishes to every registered sink, so one failing subscriber cannot silence the others.</summary>
public sealed class ConnectorRuntimePublisher
{
    private readonly IReadOnlyList<IConnectorRuntimeEventSink> _sinks;

    /// <summary>Initializes a new instance of the <see cref="ConnectorRuntimePublisher"/> class.</summary>
    /// <param name="sinks">The sinks to publish to.</param>
    public ConnectorRuntimePublisher(IEnumerable<IConnectorRuntimeEventSink> sinks)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        _sinks = [.. sinks];
    }

    /// <summary>Publishes an event to every sink.</summary>
    /// <param name="runtimeEvent">The event.</param>
    public void Publish(ConnectorRuntimeEvent runtimeEvent)
    {
        ArgumentNullException.ThrowIfNull(runtimeEvent);

        foreach (var sink in _sinks)
        {
            sink.Publish(runtimeEvent);
        }
    }
}
