using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugins.Forms.Engine.Configuration;
using FactoryOS.Plugins.Forms.Engine.Domain;
using FactoryOS.Plugins.Forms.Engine.Diagnostics;
using FactoryOS.Plugins.Forms.Engine.Events;
using FactoryOS.Plugins.Forms.Engine.Persistence;

namespace FactoryOS.Plugins.Forms.Engine.Execution;

/// <summary>
/// Saves in-progress form values as a draft without validating them, so a partly filled form can be put down
/// and picked up later. Persists the merged values, recomputes calculated fields, and publishes
/// <see cref="FormSaved"/>.
/// </summary>
public sealed class DraftService
{
    private readonly IFormStore _store;
    private readonly IFormRepository _repository;
    private readonly IFormEventSink _events;
    private readonly FormExecutor _executor;
    private readonly FormMetrics _metrics;
    private readonly FormEngineOptions _options;
    private readonly IDateTimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="DraftService"/> class.</summary>
    /// <param name="store">The instance store.</param>
    /// <param name="repository">The definition repository.</param>
    /// <param name="events">The event sink.</param>
    /// <param name="executor">The form executor.</param>
    /// <param name="metrics">The engine metrics.</param>
    /// <param name="options">The engine options.</param>
    /// <param name="clock">The clock.</param>
    public DraftService(
        IFormStore store,
        IFormRepository repository,
        IFormEventSink events,
        FormExecutor executor,
        FormMetrics metrics,
        FormEngineOptions options,
        IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        _store = store;
        _repository = repository;
        _events = events;
        _executor = executor;
        _metrics = metrics;
        _options = options;
        _clock = clock;
    }

    /// <summary>Saves a draft of an instance.</summary>
    /// <param name="instanceId">The instance id.</param>
    /// <param name="values">The values to merge into the draft.</param>
    /// <param name="cancellationToken">A token to cancel the save.</param>
    /// <returns>The updated instance, or <see langword="null"/> when the instance is unknown.</returns>
    public Task<FormInstance?> SaveDraftAsync(
        Guid instanceId,
        IReadOnlyDictionary<string, object?> values,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        cancellationToken.ThrowIfCancellationRequested();

        var instance = _store.Get(instanceId);
        if (instance is null)
        {
            return Task.FromResult<FormInstance?>(null);
        }

        var definition = _repository.Get(instance.FormKey, instance.Version)
            ?? throw new InvalidOperationException(
                $"Form definition '{instance.FormKey}' {instance.Version} is not registered.");

        var now = _clock.UtcNow;
        _executor.ApplyDraft(definition, instance, values, now, _options.TrackDraftState);
        _store.Save(instance);
        _metrics.RecordDraftSaved();
        _events.Publish(new FormSaved(instance.Tenant, now, instance.FormKey, instance.Id));
        return Task.FromResult<FormInstance?>(instance);
    }
}
