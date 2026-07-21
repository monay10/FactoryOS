using FactoryOS.Plugins.Workflow.Tasks.Configuration;
using FactoryOS.Plugins.Workflow.Tasks.Domain;
using FactoryOS.Plugins.Workflow.Tasks.Persistence;

namespace FactoryOS.Plugins.Workflow.Tasks.Execution;

/// <summary>
/// The public entry point to the human task engine. It creates tasks (standalone or bound to a workflow
/// activity), opens, completes, rejects, cancels, reassigns and comments on them, adds attachment references,
/// runs the due-work pass, and reads tasks, summaries and history back. It composes the runtime and the
/// completion / cancellation services; it never modifies the workflow runtime, only advances or cancels it
/// through the bridge.
/// </summary>
public sealed class HumanTaskEngine
{
    private readonly HumanTaskRuntime _runtime;
    private readonly TaskCompletionService _completion;
    private readonly TaskCancellationService _cancellation;
    private readonly IHumanTaskStore _store;
    private readonly IHumanTaskHistoryRepository _history;

    /// <summary>Initializes a new instance of the <see cref="HumanTaskEngine"/> class.</summary>
    /// <param name="runtime">The task runtime.</param>
    /// <param name="completion">The completion service.</param>
    /// <param name="cancellation">The cancellation service.</param>
    /// <param name="store">The task store.</param>
    /// <param name="history">The history repository.</param>
    public HumanTaskEngine(
        HumanTaskRuntime runtime,
        TaskCompletionService completion,
        TaskCancellationService cancellation,
        IHumanTaskStore store,
        IHumanTaskHistoryRepository history)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(completion);
        ArgumentNullException.ThrowIfNull(cancellation);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(history);
        _runtime = runtime;
        _completion = completion;
        _cancellation = cancellation;
        _store = store;
        _history = history;
    }

    /// <summary>Registers a task definition.</summary>
    /// <param name="definition">The definition.</param>
    public void Register(HumanTaskDefinition definition) => _runtime.Register(definition);

    /// <summary>Creates a standalone task.</summary>
    /// <param name="definition">The task definition.</param>
    /// <param name="context">The task context.</param>
    /// <param name="values">Values used to resolve a dynamic assignment.</param>
    /// <returns>The created task.</returns>
    public Task<HumanTaskInstance> CreateAsync(
        HumanTaskDefinition definition,
        HumanTaskContext context,
        IReadOnlyDictionary<string, object?>? values = null) =>
        Task.FromResult(_runtime.Create(definition, context, values));

    /// <summary>Creates a task bound to a workflow activity; completing it advances the workflow.</summary>
    /// <param name="definition">The task definition.</param>
    /// <param name="context">The task context.</param>
    /// <param name="workflowInstanceId">The workflow instance id.</param>
    /// <param name="activityNodeId">The workflow activity node id the task satisfies.</param>
    /// <param name="values">Values used to resolve a dynamic assignment (e.g. workflow variables).</param>
    /// <returns>The created, workflow-bound task.</returns>
    public Task<HumanTaskInstance> CreateForActivityAsync(
        HumanTaskDefinition definition,
        HumanTaskContext context,
        Guid workflowInstanceId,
        string activityNodeId,
        IReadOnlyDictionary<string, object?>? values = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activityNodeId);
        return Task.FromResult(_runtime.Create(definition, context, values, workflowInstanceId, activityNodeId));
    }

    /// <summary>Opens a task for its assignee.</summary>
    /// <param name="taskId">The task id.</param>
    /// <param name="actor">Who is opening it.</param>
    /// <returns>The updated task, or <see langword="null"/> when unknown.</returns>
    public Task<HumanTaskInstance?> OpenAsync(Guid taskId, string? actor = null) =>
        Task.FromResult(_runtime.Open(taskId, actor));

    /// <summary>Completes a task with a decision.</summary>
    /// <param name="taskId">The task id.</param>
    /// <param name="decision">The decision.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The completed task, or <see langword="null"/> when unknown.</returns>
    public Task<HumanTaskInstance?> CompleteAsync(
        Guid taskId, HumanTaskDecision decision, CancellationToken cancellationToken = default) =>
        _completion.CompleteAsync(taskId, decision, cancellationToken);

    /// <summary>Approves a task (a completion with an approval decision).</summary>
    /// <param name="taskId">The task id.</param>
    /// <param name="decidedBy">Who approved.</param>
    /// <param name="variables">Optional outcome variables passed to the workflow.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The completed task, or <see langword="null"/> when unknown.</returns>
    public Task<HumanTaskInstance?> ApproveAsync(
        Guid taskId,
        string? decidedBy = null,
        IReadOnlyDictionary<string, object?>? variables = null,
        CancellationToken cancellationToken = default) =>
        _completion.CompleteAsync(taskId, HumanTaskDecision.Approve(decidedBy, variables), cancellationToken);

    /// <summary>Rejects a task.</summary>
    /// <param name="taskId">The task id.</param>
    /// <param name="decidedBy">Who rejected.</param>
    /// <param name="comment">An optional reason.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The rejected task, or <see langword="null"/> when unknown.</returns>
    public Task<HumanTaskInstance?> RejectAsync(
        Guid taskId,
        string? decidedBy = null,
        string? comment = null,
        CancellationToken cancellationToken = default) =>
        _completion.RejectAsync(taskId, HumanTaskDecision.Reject(decidedBy, comment), cancellationToken);

    /// <summary>Cancels a task, optionally cancelling its owning workflow instance.</summary>
    /// <param name="taskId">The task id.</param>
    /// <param name="actor">Who is cancelling it.</param>
    /// <param name="reason">An optional reason.</param>
    /// <param name="cancelWorkflow">Whether to cancel the owning workflow instance for a bound task.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The cancelled task, or <see langword="null"/> when unknown.</returns>
    public Task<HumanTaskInstance?> CancelAsync(
        Guid taskId,
        string? actor = null,
        string? reason = null,
        bool cancelWorkflow = true,
        CancellationToken cancellationToken = default) =>
        _cancellation.CancelAsync(taskId, actor, reason, cancelWorkflow, cancellationToken);

    /// <summary>Reassigns a task to a new owner.</summary>
    /// <param name="taskId">The task id.</param>
    /// <param name="assignee">The new assignee.</param>
    /// <param name="actor">Who is reassigning it.</param>
    /// <returns>The updated task, or <see langword="null"/> when unknown.</returns>
    public Task<HumanTaskInstance?> ReassignAsync(Guid taskId, string assignee, string? actor = null) =>
        Task.FromResult(_runtime.Reassign(taskId, assignee, actor));

    /// <summary>Adds a comment to a task.</summary>
    /// <param name="taskId">The task id.</param>
    /// <param name="author">The comment author.</param>
    /// <param name="text">The comment text.</param>
    /// <param name="visibility">The comment visibility.</param>
    /// <returns>The added comment, or <see langword="null"/> when the task is unknown.</returns>
    public Task<HumanTaskComment?> AddCommentAsync(
        Guid taskId, string author, string text, CommentVisibility visibility = CommentVisibility.Public) =>
        Task.FromResult(_runtime.AddComment(taskId, author, text, visibility));

    /// <summary>Adds an attachment reference to a task.</summary>
    /// <param name="taskId">The task id.</param>
    /// <param name="fileName">The file name.</param>
    /// <param name="storageKey">The object-storage key or URI.</param>
    /// <param name="contentType">The MIME type.</param>
    /// <param name="sizeBytes">The size in bytes, if known.</param>
    /// <param name="addedBy">Who attached it.</param>
    /// <returns>The added attachment, or <see langword="null"/> when the task is unknown.</returns>
    public Task<HumanTaskAttachment?> AddAttachmentAsync(
        Guid taskId,
        string fileName,
        string storageKey,
        string? contentType = null,
        long? sizeBytes = null,
        string? addedBy = null) =>
        Task.FromResult(_runtime.AddAttachment(taskId, fileName, storageKey, contentType, sizeBytes, addedBy));

    /// <summary>Runs the due-work pass (reminders, escalations, expiries) over open tasks.</summary>
    /// <param name="cancellationToken">A token to cancel the pass.</param>
    /// <returns>A summary of what the pass did.</returns>
    public Task<DueWorkSummary> RunDueAsync(CancellationToken cancellationToken = default) =>
        _runtime.RunDueAsync(cancellationToken);

    /// <summary>Gets a task by id.</summary>
    /// <param name="taskId">The task id.</param>
    /// <returns>The task, or <see langword="null"/> when not found.</returns>
    public HumanTaskInstance? GetTask(Guid taskId) => _store.Get(taskId);

    /// <summary>Lists the open and finished tasks assigned to a principal as summaries.</summary>
    /// <param name="assignee">The assignee.</param>
    /// <returns>The task summaries.</returns>
    public IReadOnlyList<HumanTask> ListByAssignee(string assignee) =>
        _store.ListByAssignee(assignee).Select(HumanTask.From).ToArray();

    /// <summary>Gets the history entries of a task, oldest first.</summary>
    /// <param name="taskId">The task id.</param>
    /// <returns>The history entries.</returns>
    public IReadOnlyList<HumanTaskHistoryEntry> GetHistory(Guid taskId) => _history.ByTask(taskId);
}
