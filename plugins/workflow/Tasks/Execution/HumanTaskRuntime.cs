using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugins.Workflow.Tasks.Configuration;
using FactoryOS.Plugins.Workflow.Tasks.Diagnostics;
using FactoryOS.Plugins.Workflow.Tasks.Domain;
using FactoryOS.Plugins.Workflow.Tasks.Events;
using FactoryOS.Plugins.Workflow.Tasks.Persistence;

namespace FactoryOS.Plugins.Workflow.Tasks.Execution;

/// <summary>
/// Coordinates the parts of the human task engine that touch persistence and the event bus around the pure
/// <see cref="HumanTaskExecutor"/>: registering definitions, creating tasks (optionally bound to a workflow
/// activity) with their deadline / reminder / escalation schedule and resolved assignment, opening and
/// reassigning them, recording comments and attachments, and running the due-work pass that fires reminders,
/// applies escalations and expires overdue tasks.
/// </summary>
public sealed class HumanTaskRuntime
{
    private readonly IHumanTaskRepository _repository;
    private readonly IHumanTaskStore _store;
    private readonly IHumanTaskHistoryRepository _history;
    private readonly IHumanTaskEventSink _events;
    private readonly HumanTaskExecutor _executor;
    private readonly AssignmentResolver _assignmentResolver;
    private readonly DeadlineEngine _deadlineEngine;
    private readonly ReminderEngine _reminderEngine;
    private readonly EscalationEngine _escalationEngine;
    private readonly HumanTaskMetrics _metrics;
    private readonly HumanTaskEngineOptions _options;
    private readonly IDateTimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="HumanTaskRuntime"/> class.</summary>
    /// <param name="repository">The definition repository.</param>
    /// <param name="store">The task store.</param>
    /// <param name="history">The history repository.</param>
    /// <param name="events">The event sink.</param>
    /// <param name="executor">The task executor.</param>
    /// <param name="assignmentResolver">The assignment resolver.</param>
    /// <param name="deadlineEngine">The deadline engine.</param>
    /// <param name="reminderEngine">The reminder engine.</param>
    /// <param name="escalationEngine">The escalation engine.</param>
    /// <param name="metrics">The engine metrics.</param>
    /// <param name="options">The engine options.</param>
    /// <param name="clock">The clock.</param>
    public HumanTaskRuntime(
        IHumanTaskRepository repository,
        IHumanTaskStore store,
        IHumanTaskHistoryRepository history,
        IHumanTaskEventSink events,
        HumanTaskExecutor executor,
        AssignmentResolver assignmentResolver,
        DeadlineEngine deadlineEngine,
        ReminderEngine reminderEngine,
        EscalationEngine escalationEngine,
        HumanTaskMetrics metrics,
        HumanTaskEngineOptions options,
        IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(assignmentResolver);
        ArgumentNullException.ThrowIfNull(deadlineEngine);
        ArgumentNullException.ThrowIfNull(reminderEngine);
        ArgumentNullException.ThrowIfNull(escalationEngine);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        _repository = repository;
        _store = store;
        _history = history;
        _events = events;
        _executor = executor;
        _assignmentResolver = assignmentResolver;
        _deadlineEngine = deadlineEngine;
        _reminderEngine = reminderEngine;
        _escalationEngine = escalationEngine;
        _metrics = metrics;
        _options = options;
        _clock = clock;
    }

    /// <summary>Registers a task definition.</summary>
    /// <param name="definition">The definition.</param>
    public void Register(HumanTaskDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _repository.Register(definition);
    }

    /// <summary>Creates a task from a definition and resolves its assignment.</summary>
    /// <param name="definition">The task definition.</param>
    /// <param name="context">The task context (tenant, user).</param>
    /// <param name="values">Values used to resolve a dynamic assignment (workflow/form values).</param>
    /// <param name="workflowInstanceId">The workflow instance id when created from a workflow activity.</param>
    /// <param name="activityNodeId">The workflow activity node id when created from a workflow activity.</param>
    /// <returns>The created, assigned task.</returns>
    public HumanTaskInstance Create(
        HumanTaskDefinition definition,
        HumanTaskContext context,
        IReadOnlyDictionary<string, object?>? values = null,
        Guid? workflowInstanceId = null,
        string? activityNodeId = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(context);

        if (_options.AutoRegisterDefinitions)
        {
            _repository.Register(definition);
        }

        var now = _clock.UtcNow;
        var instance = HumanTaskInstance.Create(
            Guid.NewGuid(), definition.Key, context.Tenant, definition.Title, definition.Category, definition.Priority);
        instance.SetForm(definition.FormKey);
        if (workflowInstanceId is Guid workflowId && activityNodeId is not null)
        {
            instance.BindToWorkflow(workflowId, activityNodeId);
        }

        _deadlineEngine.Schedule(definition, instance, now);
        Track(_executor.Created(instance, now, context.User));
        _metrics.RecordCreated();
        _events.Publish(new HumanTaskCreated(instance.Id, instance.Tenant, now, instance.DefinitionKey));

        var assignment = _assignmentResolver.Resolve(definition.Assignment, values);
        Track(_executor.Assign(instance, assignment.Assignee, assignment.Candidates, now));
        _events.Publish(new HumanTaskAssigned(
            instance.Id, instance.Tenant, now, instance.DefinitionKey, assignment.Assignee));

        _store.Save(instance);
        return instance;
    }

    /// <summary>Opens a task for its assignee.</summary>
    /// <param name="taskId">The task id.</param>
    /// <param name="actor">Who is opening it.</param>
    /// <returns>The updated task, or <see langword="null"/> when unknown.</returns>
    public HumanTaskInstance? Open(Guid taskId, string? actor)
    {
        var instance = _store.Get(taskId);
        if (instance is null)
        {
            return null;
        }

        var now = _clock.UtcNow;
        Track(_executor.Open(instance, now, actor));
        _store.Save(instance);
        _events.Publish(new HumanTaskOpened(instance.Id, instance.Tenant, now, instance.DefinitionKey));
        return instance;
    }

    /// <summary>Reassigns a task to a new owner.</summary>
    /// <param name="taskId">The task id.</param>
    /// <param name="assignee">The new assignee.</param>
    /// <param name="actor">Who is reassigning it.</param>
    /// <returns>The updated task, or <see langword="null"/> when unknown.</returns>
    public HumanTaskInstance? Reassign(Guid taskId, string assignee, string? actor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assignee);
        var instance = _store.Get(taskId);
        if (instance is null)
        {
            return null;
        }

        var now = _clock.UtcNow;
        Track(_executor.Reassign(instance, assignee, now, actor));
        _store.Save(instance);
        _metrics.RecordReassigned();
        _events.Publish(new HumanTaskReassigned(instance.Id, instance.Tenant, now, instance.DefinitionKey, assignee));
        return instance;
    }

    /// <summary>Adds a comment to a task.</summary>
    /// <param name="taskId">The task id.</param>
    /// <param name="author">The comment author.</param>
    /// <param name="text">The comment text.</param>
    /// <param name="visibility">The comment visibility.</param>
    /// <returns>The added comment, or <see langword="null"/> when the task is unknown.</returns>
    public HumanTaskComment? AddComment(Guid taskId, string author, string text, CommentVisibility visibility)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(author);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var instance = _store.Get(taskId);
        if (instance is null)
        {
            return null;
        }

        var now = _clock.UtcNow;
        var comment = new HumanTaskComment(Guid.NewGuid(), author, text, visibility, now);
        Track(_executor.Comment(instance, comment, now));
        _store.Save(instance);
        return comment;
    }

    /// <summary>Adds an attachment reference to a task.</summary>
    /// <param name="taskId">The task id.</param>
    /// <param name="fileName">The file name.</param>
    /// <param name="storageKey">The object-storage key or URI.</param>
    /// <param name="contentType">The MIME type.</param>
    /// <param name="sizeBytes">The size in bytes, if known.</param>
    /// <param name="addedBy">Who attached it.</param>
    /// <returns>The added attachment, or <see langword="null"/> when the task is unknown.</returns>
    public HumanTaskAttachment? AddAttachment(
        Guid taskId, string fileName, string storageKey, string? contentType, long? sizeBytes, string? addedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        var instance = _store.Get(taskId);
        if (instance is null)
        {
            return null;
        }

        var now = _clock.UtcNow;
        var attachment = new HumanTaskAttachment(
            Guid.NewGuid(), fileName, storageKey, contentType, sizeBytes, addedBy, now);
        Track(_executor.Attach(instance, attachment, now));
        _store.Save(instance);
        return attachment;
    }

    /// <summary>
    /// Runs a due-work pass over every open task: fires due reminders, applies due escalations (resolving the
    /// escalation target), and expires overdue tasks that have no remaining escalation.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the pass.</param>
    /// <returns>A summary of what the pass did.</returns>
    public Task<DueWorkSummary> RunDueAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = _clock.UtcNow;
        var reminders = 0;
        var escalations = 0;
        var expirations = 0;

        foreach (var instance in _store.ListOpen().Take(_options.DueWorkBatchSize))
        {
            var changed = false;

            foreach (var _ in _reminderEngine.Fire(instance, now))
            {
                Track(_executor.Reminded(instance, now));
                _metrics.RecordReminder();
                reminders++;
                changed = true;
            }

            foreach (var escalation in _escalationEngine.DueEscalations(instance, now))
            {
                var outcome = _assignmentResolver.Resolve(escalation.To);
                instance.MarkEscalationApplied(escalation);
                Track(_executor.Escalate(instance, outcome.Assignee, now));
                _metrics.RecordEscalated();
                _events.Publish(new HumanTaskEscalated(
                    instance.Id, instance.Tenant, now, instance.DefinitionKey, instance.EscalationLevel, outcome.Assignee));
                escalations++;
                changed = true;
            }

            if (_options.ExpireOverdueTasks && _escalationEngine.IsExpired(instance, now))
            {
                Track(_executor.Expire(instance, now));
                _metrics.RecordExpired();
                _events.Publish(new HumanTaskExpired(instance.Id, instance.Tenant, now, instance.DefinitionKey));
                expirations++;
                changed = true;
            }

            if (changed)
            {
                _store.Save(instance);
            }
        }

        return Task.FromResult(new DueWorkSummary(reminders, escalations, expirations));
    }

    private void Track(HumanTaskHistoryEntry entry) => _history.Append(entry);
}

/// <summary>A summary of what a due-work pass did.</summary>
/// <param name="RemindersFired">How many reminders fired.</param>
/// <param name="EscalationsApplied">How many escalations were applied.</param>
/// <param name="TasksExpired">How many tasks were expired.</param>
public sealed record DueWorkSummary(int RemindersFired, int EscalationsApplied, int TasksExpired);
