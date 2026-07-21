using System.Collections.Concurrent;
using FactoryOS.Plugins.Workflow.SLA.Domain;

namespace FactoryOS.Plugins.Workflow.SLA.Persistence;

/// <summary>The registry of SLA definitions, keyed by definition key.</summary>
public interface ISlaRepository
{
    /// <summary>Registers a definition (idempotent by key; last registration wins).</summary>
    /// <param name="definition">The definition to register.</param>
    void Register(SlaDefinition definition);

    /// <summary>Gets a definition by key.</summary>
    /// <param name="key">The definition key.</param>
    /// <returns>The definition, or <see langword="null"/> when not registered.</returns>
    SlaDefinition? Get(string key);

    /// <summary>Gets every registered definition.</summary>
    /// <returns>The definitions.</returns>
    IReadOnlyCollection<SlaDefinition> All();
}

/// <summary>An in-memory <see cref="ISlaRepository"/>.</summary>
public sealed class InMemorySlaRepository : ISlaRepository
{
    private readonly ConcurrentDictionary<string, SlaDefinition> _definitions = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void Register(SlaDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _definitions[definition.Key] = definition;
    }

    /// <inheritdoc />
    public SlaDefinition? Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _definitions.TryGetValue(key, out var definition) ? definition : null;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<SlaDefinition> All() => _definitions.Values.ToArray();
}

/// <summary>The persistence store for SLA instances.</summary>
public interface ISlaStore
{
    /// <summary>Saves an SLA (insert or update by id).</summary>
    /// <param name="sla">The SLA to save.</param>
    void Save(SlaInstance sla);

    /// <summary>Gets an SLA by id.</summary>
    /// <param name="id">The SLA id.</param>
    /// <returns>The SLA, or <see langword="null"/> when not found.</returns>
    SlaInstance? Get(Guid id);

    /// <summary>Gets the open SLA tracking a target, if there is one.</summary>
    /// <param name="target">The tracked work.</param>
    /// <returns>The SLA, or <see langword="null"/> when nothing open tracks it.</returns>
    SlaInstance? ByTarget(SlaTarget target);

    /// <summary>Lists the SLAs in a given status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The matching SLAs.</returns>
    IReadOnlyCollection<SlaInstance> ListByStatus(SlaStatus status);

    /// <summary>Lists every SLA that has not finished.</summary>
    /// <returns>The open SLAs.</returns>
    IReadOnlyCollection<SlaInstance> ListOpen();
}

/// <summary>An in-memory <see cref="ISlaStore"/>. SLAs are held by reference, so saves are updates.</summary>
public sealed class InMemorySlaStore : ISlaStore
{
    private readonly ConcurrentDictionary<Guid, SlaInstance> _instances = new();

    /// <inheritdoc />
    public void Save(SlaInstance sla)
    {
        ArgumentNullException.ThrowIfNull(sla);
        _instances[sla.Id] = sla;
    }

    /// <inheritdoc />
    public SlaInstance? Get(Guid id) => _instances.TryGetValue(id, out var sla) ? sla : null;

    /// <inheritdoc />
    public SlaInstance? ByTarget(SlaTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return _instances.Values.FirstOrDefault(sla => sla.IsOpen && sla.Target == target);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<SlaInstance> ListByStatus(SlaStatus status) =>
        _instances.Values.Where(sla => sla.Status == status).ToArray();

    /// <inheritdoc />
    public IReadOnlyCollection<SlaInstance> ListOpen() => _instances.Values.Where(sla => sla.IsOpen).ToArray();
}

/// <summary>The persistence store for SLA history entries, queryable independently of the SLA.</summary>
public interface ISlaHistoryRepository
{
    /// <summary>Appends a history entry.</summary>
    /// <param name="entry">The entry.</param>
    void Append(SlaHistoryEntry entry);

    /// <summary>Lists the history entries for an SLA, oldest first.</summary>
    /// <param name="slaId">The SLA id.</param>
    /// <returns>The entries.</returns>
    IReadOnlyList<SlaHistoryEntry> BySla(Guid slaId);
}

/// <summary>An in-memory <see cref="ISlaHistoryRepository"/>.</summary>
public sealed class InMemorySlaHistoryRepository : ISlaHistoryRepository
{
    private readonly ConcurrentDictionary<Guid, List<SlaHistoryEntry>> _bySla = new();

    /// <inheritdoc />
    public void Append(SlaHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var list = _bySla.GetOrAdd(entry.SlaId, _ => []);
        lock (list)
        {
            list.Add(entry);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<SlaHistoryEntry> BySla(Guid slaId)
    {
        if (!_bySla.TryGetValue(slaId, out var list))
        {
            return [];
        }

        lock (list)
        {
            return list.OrderBy(entry => entry.OccurredOnUtc).ToArray();
        }
    }
}

/// <summary>The registry of business and holiday calendars an SLA policy resolves its clock from.</summary>
public interface ISlaCalendarRepository
{
    /// <summary>Registers a business calendar (idempotent by key; last registration wins).</summary>
    /// <param name="calendar">The calendar.</param>
    void Register(BusinessCalendar calendar);

    /// <summary>Gets a business calendar by key.</summary>
    /// <param name="key">The calendar key.</param>
    /// <returns>The calendar, or <see langword="null"/> when not registered.</returns>
    BusinessCalendar? Get(string key);

    /// <summary>Gets every registered business calendar.</summary>
    /// <returns>The calendars.</returns>
    IReadOnlyCollection<BusinessCalendar> All();
}

/// <summary>An in-memory <see cref="ISlaCalendarRepository"/>.</summary>
public sealed class InMemorySlaCalendarRepository : ISlaCalendarRepository
{
    private readonly ConcurrentDictionary<string, BusinessCalendar> _calendars = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void Register(BusinessCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        _calendars[calendar.Key] = calendar;
    }

    /// <inheritdoc />
    public BusinessCalendar? Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _calendars.TryGetValue(key, out var calendar) ? calendar : null;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<BusinessCalendar> All() => _calendars.Values.ToArray();
}
