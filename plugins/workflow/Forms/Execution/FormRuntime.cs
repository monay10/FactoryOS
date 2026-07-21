using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugins.Forms.Engine.Configuration;
using FactoryOS.Plugins.Forms.Engine.Domain;
using FactoryOS.Plugins.Forms.Engine.Diagnostics;
using FactoryOS.Plugins.Forms.Engine.Events;
using FactoryOS.Plugins.Forms.Engine.Persistence;

namespace FactoryOS.Plugins.Forms.Engine.Execution;

/// <summary>
/// Coordinates the parts of the forms engine that touch persistence and the event bus around the pure
/// <see cref="FormExecutor"/>: registering and versioning definitions, opening instances (optionally bound to
/// a workflow activity), and resolving submitted instances through approval, rejection or cancellation.
/// </summary>
public sealed class FormRuntime
{
    private readonly IFormRepository _repository;
    private readonly IFormStore _store;
    private readonly IFormVersionRepository _versions;
    private readonly IFormEventSink _events;
    private readonly FormExecutor _executor;
    private readonly FormMetrics _metrics;
    private readonly FormEngineOptions _options;
    private readonly IDateTimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="FormRuntime"/> class.</summary>
    /// <param name="repository">The definition repository.</param>
    /// <param name="store">The instance store.</param>
    /// <param name="versions">The version repository.</param>
    /// <param name="events">The event sink.</param>
    /// <param name="executor">The form executor.</param>
    /// <param name="metrics">The engine metrics.</param>
    /// <param name="options">The engine options.</param>
    /// <param name="clock">The clock.</param>
    public FormRuntime(
        IFormRepository repository,
        IFormStore store,
        IFormVersionRepository versions,
        IFormEventSink events,
        FormExecutor executor,
        FormMetrics metrics,
        FormEngineOptions options,
        IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(versions);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        _repository = repository;
        _store = store;
        _versions = versions;
        _events = events;
        _executor = executor;
        _metrics = metrics;
        _options = options;
        _clock = clock;
    }

    /// <summary>Registers a form definition, recording its version and publishing a created/updated event.</summary>
    /// <param name="definition">The definition to register.</param>
    public void Register(FormDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var firstVersion = _repository.GetLatest(definition.Key) is null;
        var registered = _repository.Register(definition);
        if (!registered)
        {
            return;
        }

        var now = _clock.UtcNow;
        _versions.Append(new FormVersionRecord(definition.Key, definition.Version, now));
        _events.Publish(firstVersion
            ? new FormCreated(FormConstants.DefaultTenant, now, definition.Key, definition.Version)
            : new FormUpdated(FormConstants.DefaultTenant, now, definition.Key, definition.Version));
    }

    /// <summary>Opens an instance of a supplied definition, optionally bound to a workflow activity.</summary>
    /// <param name="definition">The definition to open.</param>
    /// <param name="context">The form context (tenant, user).</param>
    /// <param name="seed">Optional seed values.</param>
    /// <param name="workflowInstanceId">The workflow instance id when opened from a workflow activity.</param>
    /// <param name="activityNodeId">The workflow activity node id when opened from a workflow activity.</param>
    /// <returns>The opened instance.</returns>
    public FormInstance Open(
        FormDefinition definition,
        FormContext context,
        IReadOnlyDictionary<string, object?>? seed = null,
        Guid? workflowInstanceId = null,
        string? activityNodeId = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(context);

        if (_options.AutoRegisterDefinitions)
        {
            Register(definition);
        }

        var instance = FormInstance.Create(
            Guid.NewGuid(),
            definition.Key,
            definition.Version,
            context.Tenant,
            seed is null ? null : new FormValues(seed));

        if (workflowInstanceId is Guid workflowId && activityNodeId is not null)
        {
            instance.BindToWorkflow(workflowId, activityNodeId);
        }

        var now = _clock.UtcNow;
        _executor.Open(definition, instance, now);
        _store.Save(instance);
        _metrics.RecordOpened();
        _events.Publish(new FormOpened(instance.Tenant, now, instance.FormKey, instance.Id, instance.Assignee));
        return instance;
    }

    /// <summary>Approves a submitted instance.</summary>
    /// <param name="instanceId">The instance id.</param>
    /// <returns>The updated instance, or <see langword="null"/> when unknown.</returns>
    public FormInstance? Approve(Guid instanceId) => Resolve(
        instanceId,
        FormInstanceState.Submitted,
        instance => instance.MarkApproved(),
        (instance, now) =>
        {
            _metrics.RecordApproved();
            _events.Publish(new FormApproved(instance.Tenant, now, instance.FormKey, instance.Id));
            instance.History.Append(new FormHistoryEntry(now, "approved", null, null));
        });

    /// <summary>Rejects a submitted instance.</summary>
    /// <param name="instanceId">The instance id.</param>
    /// <param name="reason">The rejection reason.</param>
    /// <returns>The updated instance, or <see langword="null"/> when unknown.</returns>
    public FormInstance? Reject(Guid instanceId, string? reason = null) => Resolve(
        instanceId,
        FormInstanceState.Submitted,
        instance => instance.MarkRejected(),
        (instance, now) =>
        {
            _metrics.RecordRejected();
            _events.Publish(new FormRejected(instance.Tenant, now, instance.FormKey, instance.Id, reason));
            instance.History.Append(new FormHistoryEntry(now, "rejected", null, reason));
        });

    /// <summary>Cancels an instance that has not yet finished.</summary>
    /// <param name="instanceId">The instance id.</param>
    /// <returns>The updated instance, or <see langword="null"/> when unknown.</returns>
    public FormInstance? Cancel(Guid instanceId)
    {
        var instance = _store.Get(instanceId);
        if (instance is null)
        {
            return null;
        }

        if (instance.IsFinished)
        {
            throw new InvalidOperationException($"Form instance '{instanceId}' is '{instance.State}' and cannot be cancelled.");
        }

        var now = _clock.UtcNow;
        instance.MarkCancelled();
        instance.History.Append(new FormHistoryEntry(now, "cancelled", null, null));
        _store.Save(instance);
        _metrics.RecordCancelled();
        _events.Publish(new FormCancelled(instance.Tenant, now, instance.FormKey, instance.Id));
        return instance;
    }

    private FormInstance? Resolve(
        Guid instanceId,
        FormInstanceState required,
        Action<FormInstance> transition,
        Action<FormInstance, DateTimeOffset> onResolved)
    {
        var instance = _store.Get(instanceId);
        if (instance is null)
        {
            return null;
        }

        if (instance.State != required)
        {
            throw new InvalidOperationException(
                $"Form instance '{instanceId}' must be '{required}' but is '{instance.State}'.");
        }

        transition(instance);
        _store.Save(instance);
        onResolved(instance, _clock.UtcNow);
        return instance;
    }
}
