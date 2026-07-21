using FactoryOS.Plugins.Forms.Engine.Configuration;
using FactoryOS.Plugins.Forms.Engine.Domain;
using FactoryOS.Plugins.Forms.Engine.Persistence;
using FactoryOS.Plugins.Forms.Engine.Rendering;

namespace FactoryOS.Plugins.Forms.Engine.Execution;

/// <summary>
/// The public entry point to the forms runtime. It opens form instances (standalone or bound to a workflow
/// activity), saves drafts, submits them through validation, resolves submissions, renders a form for a UI,
/// and reads instances and submissions back. It composes the runtime, draft and submission services; it never
/// modifies the workflow runtime, only advances it through the bridge on a valid submission.
/// </summary>
public sealed class FormEngine
{
    private readonly FormRuntime _runtime;
    private readonly DraftService _draftService;
    private readonly SubmissionService _submissionService;
    private readonly FormRenderer _renderer;
    private readonly IFormStore _store;
    private readonly IFormRepository _repository;
    private readonly IFormSubmissionRepository _submissions;

    /// <summary>Initializes a new instance of the <see cref="FormEngine"/> class.</summary>
    /// <param name="runtime">The form runtime.</param>
    /// <param name="draftService">The draft service.</param>
    /// <param name="submissionService">The submission service.</param>
    /// <param name="renderer">The form renderer.</param>
    /// <param name="store">The instance store.</param>
    /// <param name="repository">The definition repository.</param>
    /// <param name="submissions">The submission repository.</param>
    public FormEngine(
        FormRuntime runtime,
        DraftService draftService,
        SubmissionService submissionService,
        FormRenderer renderer,
        IFormStore store,
        IFormRepository repository,
        IFormSubmissionRepository submissions)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(draftService);
        ArgumentNullException.ThrowIfNull(submissionService);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(submissions);
        _runtime = runtime;
        _draftService = draftService;
        _submissionService = submissionService;
        _renderer = renderer;
        _store = store;
        _repository = repository;
        _submissions = submissions;
    }

    /// <summary>Registers a form definition so instances can be opened from it.</summary>
    /// <param name="definition">The definition to register.</param>
    public void Register(FormDefinition definition) => _runtime.Register(definition);

    /// <summary>Opens a standalone instance of a form.</summary>
    /// <param name="definition">The definition to open.</param>
    /// <param name="context">The form context (tenant, user).</param>
    /// <param name="seed">Optional seed values.</param>
    /// <returns>The opened instance.</returns>
    public Task<FormInstance> OpenAsync(
        FormDefinition definition,
        FormContext context,
        IReadOnlyDictionary<string, object?>? seed = null) =>
        Task.FromResult(_runtime.Open(definition, context, seed));

    /// <summary>
    /// Opens an instance bound to a workflow activity. When later submitted with valid values, the bound
    /// activity completes and the workflow advances.
    /// </summary>
    /// <param name="definition">The definition to open.</param>
    /// <param name="context">The form context (tenant, user).</param>
    /// <param name="workflowInstanceId">The workflow instance id.</param>
    /// <param name="activityNodeId">The workflow activity node id the form satisfies.</param>
    /// <param name="seed">Optional seed values (e.g. copied from workflow variables).</param>
    /// <returns>The opened, workflow-bound instance.</returns>
    public Task<FormInstance> OpenForActivityAsync(
        FormDefinition definition,
        FormContext context,
        Guid workflowInstanceId,
        string activityNodeId,
        IReadOnlyDictionary<string, object?>? seed = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activityNodeId);
        return Task.FromResult(_runtime.Open(definition, context, seed, workflowInstanceId, activityNodeId));
    }

    /// <summary>Saves a draft of an instance without validating.</summary>
    /// <param name="instanceId">The instance id.</param>
    /// <param name="values">The values to merge into the draft.</param>
    /// <param name="cancellationToken">A token to cancel the save.</param>
    /// <returns>The updated instance, or <see langword="null"/> when unknown.</returns>
    public Task<FormInstance?> SaveDraftAsync(
        Guid instanceId,
        IReadOnlyDictionary<string, object?> values,
        CancellationToken cancellationToken = default) =>
        _draftService.SaveDraftAsync(instanceId, values, cancellationToken);

    /// <summary>Submits an instance; blocks and returns validation errors when the values are invalid.</summary>
    /// <param name="instanceId">The instance id.</param>
    /// <param name="values">The final values to apply before validating.</param>
    /// <param name="submittedBy">Who is submitting.</param>
    /// <param name="cancellationToken">A token to cancel the submission.</param>
    /// <returns>The submission result, or <see langword="null"/> when unknown.</returns>
    public Task<SubmissionResult?> SubmitAsync(
        Guid instanceId,
        IReadOnlyDictionary<string, object?> values,
        string? submittedBy = null,
        CancellationToken cancellationToken = default) =>
        _submissionService.SubmitAsync(instanceId, values, submittedBy, cancellationToken);

    /// <summary>Approves a submitted instance.</summary>
    /// <param name="instanceId">The instance id.</param>
    /// <returns>The updated instance, or <see langword="null"/> when unknown.</returns>
    public Task<FormInstance?> ApproveAsync(Guid instanceId) => Task.FromResult(_runtime.Approve(instanceId));

    /// <summary>Rejects a submitted instance.</summary>
    /// <param name="instanceId">The instance id.</param>
    /// <param name="reason">The rejection reason.</param>
    /// <returns>The updated instance, or <see langword="null"/> when unknown.</returns>
    public Task<FormInstance?> RejectAsync(Guid instanceId, string? reason = null) =>
        Task.FromResult(_runtime.Reject(instanceId, reason));

    /// <summary>Cancels an unfinished instance.</summary>
    /// <param name="instanceId">The instance id.</param>
    /// <returns>The updated instance, or <see langword="null"/> when unknown.</returns>
    public Task<FormInstance?> CancelAsync(Guid instanceId) => Task.FromResult(_runtime.Cancel(instanceId));

    /// <summary>Renders an instance into a layout model for a UI.</summary>
    /// <param name="instanceId">The instance id.</param>
    /// <returns>The rendered form, or <see langword="null"/> when the instance or its definition is unknown.</returns>
    public RenderedForm? Render(Guid instanceId)
    {
        var instance = _store.Get(instanceId);
        if (instance is null)
        {
            return null;
        }

        var definition = _repository.Get(instance.FormKey, instance.Version);
        return definition is null ? null : _renderer.Render(definition, instance);
    }

    /// <summary>Gets an instance by id.</summary>
    /// <param name="instanceId">The instance id.</param>
    /// <returns>The instance, or <see langword="null"/> when not found.</returns>
    public FormInstance? GetInstance(Guid instanceId) => _store.Get(instanceId);

    /// <summary>Gets the submissions captured for an instance, oldest first.</summary>
    /// <param name="instanceId">The instance id.</param>
    /// <returns>The submissions.</returns>
    public IReadOnlyList<FormSubmission> GetSubmissions(Guid instanceId) => _submissions.ListByInstance(instanceId);
}
