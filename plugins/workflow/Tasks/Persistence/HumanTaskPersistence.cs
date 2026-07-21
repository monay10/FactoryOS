using System.Collections.Concurrent;
using FactoryOS.Plugins.Workflow.Tasks.Domain;

namespace FactoryOS.Plugins.Workflow.Tasks.Persistence;

/// <summary>The registry of human task definitions, keyed by definition key.</summary>
public interface IHumanTaskRepository
{
    /// <summary>Registers a definition (idempotent by key; last registration wins).</summary>
    /// <param name="definition">The definition to register.</param>
    void Register(HumanTaskDefinition definition);

    /// <summary>Gets a definition by key.</summary>
    /// <param name="key">The definition key.</param>
    /// <returns>The definition, or <see langword="null"/> when not registered.</returns>
    HumanTaskDefinition? Get(string key);

    /// <summary>Gets every registered definition.</summary>
    /// <returns>The definitions.</returns>
    IReadOnlyCollection<HumanTaskDefinition> All();
}

/// <summary>An in-memory <see cref="IHumanTaskRepository"/>.</summary>
public sealed class InMemoryHumanTaskRepository : IHumanTaskRepository
{
    private readonly ConcurrentDictionary<string, HumanTaskDefinition> _definitions = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void Register(HumanTaskDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _definitions[definition.Key] = definition;
    }

    /// <inheritdoc />
    public HumanTaskDefinition? Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _definitions.TryGetValue(key, out var definition) ? definition : null;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<HumanTaskDefinition> All() => _definitions.Values.ToArray();
}

/// <summary>The persistence store for human task instances.</summary>
public interface IHumanTaskStore
{
    /// <summary>Saves a task (insert or update by id).</summary>
    /// <param name="task">The task to save.</param>
    void Save(HumanTaskInstance task);

    /// <summary>Gets a task by id.</summary>
    /// <param name="id">The task id.</param>
    /// <returns>The task, or <see langword="null"/> when not found.</returns>
    HumanTaskInstance? Get(Guid id);

    /// <summary>Lists the tasks assigned to a principal.</summary>
    /// <param name="assignee">The assignee.</param>
    /// <returns>The assignee's tasks.</returns>
    IReadOnlyCollection<HumanTaskInstance> ListByAssignee(string assignee);

    /// <summary>Lists the tasks of a tenant in a given status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The matching tasks.</returns>
    IReadOnlyCollection<HumanTaskInstance> ListByStatus(HumanTaskStatus status);

    /// <summary>Lists every task that has not reached a terminal status.</summary>
    /// <returns>The open tasks.</returns>
    IReadOnlyCollection<HumanTaskInstance> ListOpen();
}

/// <summary>An in-memory <see cref="IHumanTaskStore"/>. Tasks are held by reference, so saves are updates.</summary>
public sealed class InMemoryHumanTaskStore : IHumanTaskStore
{
    private readonly ConcurrentDictionary<Guid, HumanTaskInstance> _tasks = new();

    /// <inheritdoc />
    public void Save(HumanTaskInstance task)
    {
        ArgumentNullException.ThrowIfNull(task);
        _tasks[task.Id] = task;
    }

    /// <inheritdoc />
    public HumanTaskInstance? Get(Guid id) => _tasks.TryGetValue(id, out var task) ? task : null;

    /// <inheritdoc />
    public IReadOnlyCollection<HumanTaskInstance> ListByAssignee(string assignee)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assignee);
        return _tasks.Values
            .Where(task => string.Equals(task.Assignee, assignee, StringComparison.Ordinal))
            .ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyCollection<HumanTaskInstance> ListByStatus(HumanTaskStatus status) =>
        _tasks.Values.Where(task => task.Status == status).ToArray();

    /// <inheritdoc />
    public IReadOnlyCollection<HumanTaskInstance> ListOpen() =>
        _tasks.Values.Where(task => !task.IsFinished).ToArray();
}

/// <summary>The persistence store for human task history entries, kept queryable independently of the task.</summary>
public interface IHumanTaskHistoryRepository
{
    /// <summary>Appends a history entry.</summary>
    /// <param name="entry">The entry.</param>
    void Append(HumanTaskHistoryEntry entry);

    /// <summary>Lists the history entries for a task, oldest first.</summary>
    /// <param name="taskId">The task id.</param>
    /// <returns>The entries.</returns>
    IReadOnlyList<HumanTaskHistoryEntry> ByTask(Guid taskId);
}

/// <summary>An in-memory <see cref="IHumanTaskHistoryRepository"/>.</summary>
public sealed class InMemoryHumanTaskHistoryRepository : IHumanTaskHistoryRepository
{
    private readonly ConcurrentDictionary<Guid, List<HumanTaskHistoryEntry>> _byTask = new();

    /// <inheritdoc />
    public void Append(HumanTaskHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var list = _byTask.GetOrAdd(entry.TaskId, _ => []);
        lock (list)
        {
            list.Add(entry);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<HumanTaskHistoryEntry> ByTask(Guid taskId)
    {
        if (!_byTask.TryGetValue(taskId, out var list))
        {
            return [];
        }

        lock (list)
        {
            return list.OrderBy(entry => entry.OccurredOnUtc).ToArray();
        }
    }
}
