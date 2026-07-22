using FactoryOS.Contracts.Plugins;
using FactoryOS.Plugin.Health;
using FactoryOS.Plugins.Runtime.Domain;

namespace FactoryOS.Plugins.Runtime.Events;

/// <summary>
/// The base of everything the plugin runtime announces. Every event carries the tenant, because there is no
/// such thing as a plugin fact that is not one factory's fact.
/// </summary>
/// <param name="Tenant">The tenant the event concerns.</param>
/// <param name="PluginKey">The plugin the event concerns.</param>
/// <param name="OccurredUtc">When it happened.</param>
public abstract record PluginRuntimeEvent(string Tenant, string PluginKey, DateTimeOffset OccurredUtc)
{
    /// <summary>Gets the event name, used as the type discriminator on the bus.</summary>
    public string Name => GetType().Name;
}

/// <summary>A package was found on disk and read successfully.</summary>
/// <param name="Tenant">The tenant the discovery ran for.</param>
/// <param name="PluginKey">The plugin found.</param>
/// <param name="OccurredUtc">When it happened.</param>
/// <param name="Version">The version found.</param>
/// <param name="Location">Where it was found.</param>
public sealed record PluginDiscovered(
    string Tenant, string PluginKey, DateTimeOffset OccurredUtc, PluginVersion Version, string? Location)
    : PluginRuntimeEvent(Tenant, PluginKey, OccurredUtc);

/// <summary>A validated package was installed for a tenant.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="PluginKey">The plugin.</param>
/// <param name="OccurredUtc">When it happened.</param>
/// <param name="Version">The version installed.</param>
/// <param name="HasSignature">Whether the package carried a signature.</param>
public sealed record PluginInstalled(
    string Tenant, string PluginKey, DateTimeOffset OccurredUtc, PluginVersion Version, bool HasSignature)
    : PluginRuntimeEvent(Tenant, PluginKey, OccurredUtc);

/// <summary>A plugin's assembly was loaded and its entry type activated.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="PluginKey">The plugin.</param>
/// <param name="OccurredUtc">When it happened.</param>
/// <param name="Version">The version loaded.</param>
/// <param name="Isolation">The isolation the assembly was loaded under.</param>
public sealed record PluginLoaded(
    string Tenant,
    string PluginKey,
    DateTimeOffset OccurredUtc,
    PluginVersion Version,
    PluginIsolationMode Isolation)
    : PluginRuntimeEvent(Tenant, PluginKey, OccurredUtc);

/// <summary>A plugin started and is accepting work.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="PluginKey">The plugin.</param>
/// <param name="OccurredUtc">When it happened.</param>
/// <param name="Version">The version running.</param>
public sealed record PluginStarted(
    string Tenant, string PluginKey, DateTimeOffset OccurredUtc, PluginVersion Version)
    : PluginRuntimeEvent(Tenant, PluginKey, OccurredUtc);

/// <summary>A plugin stopped.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="PluginKey">The plugin.</param>
/// <param name="OccurredUtc">When it happened.</param>
/// <param name="Version">The version that stopped.</param>
public sealed record PluginStopped(
    string Tenant, string PluginKey, DateTimeOffset OccurredUtc, PluginVersion Version)
    : PluginRuntimeEvent(Tenant, PluginKey, OccurredUtc);

/// <summary>A plugin was suspended: still loaded, holding its state, refusing new work.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="PluginKey">The plugin.</param>
/// <param name="OccurredUtc">When it happened.</param>
/// <param name="Reason">Why it was suspended.</param>
public sealed record PluginSuspended(
    string Tenant, string PluginKey, DateTimeOffset OccurredUtc, string Reason)
    : PluginRuntimeEvent(Tenant, PluginKey, OccurredUtc);

/// <summary>A suspended plugin returned to service.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="PluginKey">The plugin.</param>
/// <param name="OccurredUtc">When it happened.</param>
public sealed record PluginResumed(string Tenant, string PluginKey, DateTimeOffset OccurredUtc)
    : PluginRuntimeEvent(Tenant, PluginKey, OccurredUtc);

/// <summary>
/// A plugin moved from one version to another. The same event carries a rollback, with the versions the
/// other way round — an operator reading the history should see one story, not two vocabularies.
/// </summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="PluginKey">The plugin.</param>
/// <param name="OccurredUtc">When it happened.</param>
/// <param name="FromVersion">The version left behind.</param>
/// <param name="ToVersion">The version now installed.</param>
/// <param name="RolledBack">Whether the move undid an earlier update.</param>
public sealed record PluginUpdated(
    string Tenant,
    string PluginKey,
    DateTimeOffset OccurredUtc,
    PluginVersion FromVersion,
    PluginVersion ToVersion,
    bool RolledBack)
    : PluginRuntimeEvent(Tenant, PluginKey, OccurredUtc);

/// <summary>A plugin was removed from a tenant.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="PluginKey">The plugin.</param>
/// <param name="OccurredUtc">When it happened.</param>
/// <param name="Version">The version removed.</param>
public sealed record PluginRemoved(
    string Tenant, string PluginKey, DateTimeOffset OccurredUtc, PluginVersion Version)
    : PluginRuntimeEvent(Tenant, PluginKey, OccurredUtc);

/// <summary>A plugin instance's derived health verdict changed.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="PluginKey">The plugin.</param>
/// <param name="OccurredUtc">When it happened.</param>
/// <param name="Previous">The verdict that no longer holds.</param>
/// <param name="Current">The verdict that now holds.</param>
/// <param name="Detail">Why it changed.</param>
public sealed record PluginHealthChanged(
    string Tenant,
    string PluginKey,
    DateTimeOffset OccurredUtc,
    PluginHealthStatus Previous,
    PluginHealthStatus Current,
    string Detail)
    : PluginRuntimeEvent(Tenant, PluginKey, OccurredUtc);

/// <summary>A lifecycle transition failed.</summary>
/// <param name="Tenant">The tenant.</param>
/// <param name="PluginKey">The plugin.</param>
/// <param name="OccurredUtc">When it happened.</param>
/// <param name="Phase">Which step failed.</param>
/// <param name="Kind">How it failed.</param>
/// <param name="Reason">Why it failed.</param>
public sealed record PluginFailed(
    string Tenant,
    string PluginKey,
    DateTimeOffset OccurredUtc,
    PluginLifecyclePhase Phase,
    PluginFailureKind Kind,
    string Reason)
    : PluginRuntimeEvent(Tenant, PluginKey, OccurredUtc);

/// <summary>
/// Where the runtime's events go. This is a <b>port</b>: the default keeps a bounded in-memory history so
/// the runtime is useful on its own, and a host substitutes the event bus, a SIEM forwarder or both.
/// </summary>
public interface IPluginRuntimeEventSink
{
    /// <summary>Receives one event.</summary>
    /// <param name="runtimeEvent">The event.</param>
    void Publish(PluginRuntimeEvent runtimeEvent);
}

/// <summary>A bounded in-memory <see cref="IPluginRuntimeEventSink"/>, the default when a host supplies none.</summary>
public sealed class InMemoryPluginRuntimeEventSink : IPluginRuntimeEventSink
{
    /// <summary>The number of events retained before the oldest are dropped.</summary>
    public const int HistoryLimit = 1000;

    private readonly Lock _gate = new();
    private readonly Queue<PluginRuntimeEvent> _events = new();

    /// <inheritdoc />
    public void Publish(PluginRuntimeEvent runtimeEvent)
    {
        ArgumentNullException.ThrowIfNull(runtimeEvent);

        lock (_gate)
        {
            _events.Enqueue(runtimeEvent);
            while (_events.Count > HistoryLimit)
            {
                _events.Dequeue();
            }
        }
    }

    /// <summary>Gets every retained event, oldest first.</summary>
    /// <returns>The events.</returns>
    public IReadOnlyList<PluginRuntimeEvent> All()
    {
        lock (_gate)
        {
            return [.. _events];
        }
    }

    /// <summary>Gets the retained events of one type.</summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <returns>The matching events, oldest first.</returns>
    public IReadOnlyList<TEvent> Of<TEvent>()
        where TEvent : PluginRuntimeEvent
    {
        lock (_gate)
        {
            return [.. _events.OfType<TEvent>()];
        }
    }

    /// <summary>Gets the retained events concerning one tenant.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>The matching events, oldest first.</returns>
    public IReadOnlyList<PluginRuntimeEvent> ForTenant(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        lock (_gate)
        {
            return [.. _events.Where(item => string.Equals(item.Tenant, tenant, StringComparison.OrdinalIgnoreCase))];
        }
    }

    /// <summary>Forgets everything retained.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _events.Clear();
        }
    }
}

/// <summary>
/// Fans one event out to every registered sink.
/// <para>
/// Fan-out rather than a single consumer is the point: observability, an event-bus forwarder and an exporter
/// are three subscribers to one stream, not three layers of decorator wrapped around one.
/// </para>
/// </summary>
public sealed class PluginRuntimePublisher
{
    private readonly IReadOnlyList<IPluginRuntimeEventSink> _sinks;

    /// <summary>Initializes a new instance of the <see cref="PluginRuntimePublisher"/> class.</summary>
    /// <param name="sinks">Every registered sink.</param>
    public PluginRuntimePublisher(IEnumerable<IPluginRuntimeEventSink> sinks)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        _sinks = [.. sinks];
    }

    /// <summary>Gets the number of sinks the publisher fans out to.</summary>
    public int SinkCount => _sinks.Count;

    /// <summary>Publishes one event to every sink.</summary>
    /// <param name="runtimeEvent">The event.</param>
    public void Publish(PluginRuntimeEvent runtimeEvent)
    {
        ArgumentNullException.ThrowIfNull(runtimeEvent);

        foreach (var sink in _sinks)
        {
            sink.Publish(runtimeEvent);
        }
    }
}
