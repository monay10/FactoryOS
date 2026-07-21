using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugins.Forms.Engine.Domain;
using FactoryOS.Plugins.Forms.Engine.Diagnostics;
using FactoryOS.Plugins.Forms.Engine.Events;
using FactoryOS.Plugins.Forms.Engine.Persistence;

namespace FactoryOS.Plugins.Forms.Engine.Execution;

/// <summary>
/// Submits a form instance: applies the final values, validates them, and — only when validation passes —
/// captures a submission, moves the instance to submitted, publishes <see cref="FormSubmitted"/>, and, when
/// the instance was opened from a workflow activity, completes that activity so the workflow advances. A
/// submission that fails validation changes no state and never touches the workflow.
/// </summary>
public sealed class SubmissionService
{
    private readonly IFormStore _store;
    private readonly IFormRepository _repository;
    private readonly IFormSubmissionRepository _submissions;
    private readonly IFormEventSink _events;
    private readonly FormExecutor _executor;
    private readonly FormMetrics _metrics;
    private readonly IDateTimeProvider _clock;
    private readonly IFormWorkflowBridge? _workflowBridge;

    /// <summary>Initializes a new instance of the <see cref="SubmissionService"/> class.</summary>
    /// <param name="store">The instance store.</param>
    /// <param name="repository">The definition repository.</param>
    /// <param name="submissions">The submission repository.</param>
    /// <param name="events">The event sink.</param>
    /// <param name="executor">The form executor.</param>
    /// <param name="metrics">The engine metrics.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="workflowBridge">The workflow bridge, when the workflow engine is available.</param>
    public SubmissionService(
        IFormStore store,
        IFormRepository repository,
        IFormSubmissionRepository submissions,
        IFormEventSink events,
        FormExecutor executor,
        FormMetrics metrics,
        IDateTimeProvider clock,
        IFormWorkflowBridge? workflowBridge = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(submissions);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(clock);
        _store = store;
        _repository = repository;
        _submissions = submissions;
        _events = events;
        _executor = executor;
        _metrics = metrics;
        _clock = clock;
        _workflowBridge = workflowBridge;
    }

    /// <summary>Submits an instance with a final set of values.</summary>
    /// <param name="instanceId">The instance id.</param>
    /// <param name="values">The final values to apply before validating.</param>
    /// <param name="submittedBy">Who is submitting.</param>
    /// <param name="cancellationToken">A token to cancel the submission.</param>
    /// <returns>
    /// The submission result — accepted or carrying validation errors — or <see langword="null"/> when the
    /// instance is unknown.
    /// </returns>
    public async Task<SubmissionResult?> SubmitAsync(
        Guid instanceId,
        IReadOnlyDictionary<string, object?> values,
        string? submittedBy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        cancellationToken.ThrowIfCancellationRequested();

        var instance = _store.Get(instanceId);
        if (instance is null)
        {
            return null;
        }

        var definition = _repository.Get(instance.FormKey, instance.Version)
            ?? throw new InvalidOperationException(
                $"Form definition '{instance.FormKey}' {instance.Version} is not registered.");

        var now = _clock.UtcNow;
        var result = _executor.TrySubmit(definition, instance, values, submittedBy, now, Guid.NewGuid());

        if (!result.IsAccepted)
        {
            instance.History.Append(new FormHistoryEntry(
                now, "submit-rejected", submittedBy, $"{result.Validation.Errors.Count} validation error(s)"));
            _store.Save(instance);
            _metrics.RecordValidationFailure();
            return result;
        }

        _submissions.Add(result.Submission!);
        _store.Save(instance);
        _metrics.RecordSubmitted();
        _events.Publish(new FormSubmitted(
            instance.Tenant, now, instance.FormKey, instance.Id, result.Submission!.Id, submittedBy));

        if (instance.IsWorkflowBound)
        {
            await CompleteWorkflowAsync(instance, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    private async Task CompleteWorkflowAsync(FormInstance instance, CancellationToken cancellationToken)
    {
        if (_workflowBridge is null)
        {
            throw new InvalidOperationException(
                $"Form instance '{instance.Id}' is bound to a workflow activity but no workflow bridge is registered.");
        }

        var outcome = new Dictionary<string, object?>(instance.Values.AsReadOnly(), StringComparer.Ordinal);
        await _workflowBridge.CompleteActivityAsync(
            instance.WorkflowInstanceId!.Value, instance.WorkflowActivityNodeId!, outcome, cancellationToken)
            .ConfigureAwait(false);
    }
}
