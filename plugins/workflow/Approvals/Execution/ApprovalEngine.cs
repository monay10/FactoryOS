using FactoryOS.Plugins.Workflow.Approvals.Configuration;
using FactoryOS.Plugins.Workflow.Approvals.Domain;
using FactoryOS.Plugins.Workflow.Approvals.Persistence;

namespace FactoryOS.Plugins.Workflow.Approvals.Execution;

/// <summary>
/// The public entry point to the approval engine. It starts approvals (standalone or bound to a workflow
/// activity), records participant votes (approve / reject), cancels approvals, comments on them, runs the
/// due-work pass, and reads approvals and history back. It composes the runtime and the decision / cancellation
/// services; it never modifies the workflow runtime, the human task engine or the forms engine — it only
/// advances or cancels the workflow through the bridge, and higher-level composition (surfacing approvals as
/// human tasks or forms) belongs to the orchestration layer that subscribes to the approval events.
/// </summary>
public sealed class ApprovalEngine
{
    private readonly ApprovalRuntime _runtime;
    private readonly ApprovalDecisionService _decision;
    private readonly ApprovalCancellationService _cancellation;
    private readonly IApprovalStore _store;
    private readonly IApprovalHistoryRepository _history;

    /// <summary>Initializes a new instance of the <see cref="ApprovalEngine"/> class.</summary>
    /// <param name="runtime">The approval runtime.</param>
    /// <param name="decision">The decision service.</param>
    /// <param name="cancellation">The cancellation service.</param>
    /// <param name="store">The approval store.</param>
    /// <param name="history">The history repository.</param>
    public ApprovalEngine(
        ApprovalRuntime runtime,
        ApprovalDecisionService decision,
        ApprovalCancellationService cancellation,
        IApprovalStore store,
        IApprovalHistoryRepository history)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(cancellation);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(history);
        _runtime = runtime;
        _decision = decision;
        _cancellation = cancellation;
        _store = store;
        _history = history;
    }

    /// <summary>Registers an approval definition.</summary>
    /// <param name="definition">The definition.</param>
    public void Register(ApprovalDefinition definition) => _runtime.Register(definition);

    /// <summary>Starts a standalone approval.</summary>
    /// <param name="definition">The approval definition.</param>
    /// <param name="context">The approval context (tenant, initiator, values).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The started approval.</returns>
    public Task<ApprovalInstance> StartAsync(
        ApprovalDefinition definition, ApprovalContext context, CancellationToken cancellationToken = default) =>
        _runtime.StartAsync(definition, context, cancellationToken: cancellationToken);

    /// <summary>Starts an approval bound to a workflow activity; finishing it advances the workflow.</summary>
    /// <param name="definition">The approval definition.</param>
    /// <param name="context">The approval context.</param>
    /// <param name="workflowInstanceId">The workflow instance id.</param>
    /// <param name="activityNodeId">The workflow activity node id the approval satisfies.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The started, workflow-bound approval.</returns>
    public Task<ApprovalInstance> StartForActivityAsync(
        ApprovalDefinition definition,
        ApprovalContext context,
        Guid workflowInstanceId,
        string activityNodeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activityNodeId);
        return _runtime.StartAsync(definition, context, workflowInstanceId, activityNodeId, cancellationToken);
    }

    /// <summary>Records a participant's vote (approve or reject) and advances the approval.</summary>
    /// <param name="approvalId">The approval id.</param>
    /// <param name="participantId">The participant casting the vote.</param>
    /// <param name="kind">Whether the participant approves or rejects.</param>
    /// <param name="decidedBy">The concrete user casting the vote.</param>
    /// <param name="comment">An optional comment.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The updated approval, or <see langword="null"/> when unknown.</returns>
    public Task<ApprovalInstance?> DecideAsync(
        Guid approvalId,
        string participantId,
        ApprovalDecisionKind kind,
        string? decidedBy = null,
        string? comment = null,
        CancellationToken cancellationToken = default) =>
        _decision.DecideAsync(approvalId, participantId, kind, decidedBy, comment, cancellationToken);

    /// <summary>Casts an approval vote for a participant.</summary>
    /// <param name="approvalId">The approval id.</param>
    /// <param name="participantId">The participant.</param>
    /// <param name="decidedBy">The concrete user.</param>
    /// <param name="comment">An optional comment.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The updated approval, or <see langword="null"/> when unknown.</returns>
    public Task<ApprovalInstance?> ApproveAsync(
        Guid approvalId,
        string participantId,
        string? decidedBy = null,
        string? comment = null,
        CancellationToken cancellationToken = default) =>
        _decision.DecideAsync(approvalId, participantId, ApprovalDecisionKind.Approve, decidedBy, comment, cancellationToken);

    /// <summary>Casts a reject vote for a participant.</summary>
    /// <param name="approvalId">The approval id.</param>
    /// <param name="participantId">The participant.</param>
    /// <param name="decidedBy">The concrete user.</param>
    /// <param name="comment">An optional reason.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The updated approval, or <see langword="null"/> when unknown.</returns>
    public Task<ApprovalInstance?> RejectAsync(
        Guid approvalId,
        string participantId,
        string? decidedBy = null,
        string? comment = null,
        CancellationToken cancellationToken = default) =>
        _decision.DecideAsync(approvalId, participantId, ApprovalDecisionKind.Reject, decidedBy, comment, cancellationToken);

    /// <summary>Cancels an approval, optionally cancelling its owning workflow instance.</summary>
    /// <param name="approvalId">The approval id.</param>
    /// <param name="actor">Who is cancelling it.</param>
    /// <param name="reason">An optional reason.</param>
    /// <param name="cancelWorkflow">Whether to cancel the owning workflow instance for a bound approval.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The cancelled approval, or <see langword="null"/> when unknown.</returns>
    public Task<ApprovalInstance?> CancelAsync(
        Guid approvalId,
        string? actor = null,
        string? reason = null,
        bool cancelWorkflow = true,
        CancellationToken cancellationToken = default) =>
        _cancellation.CancelAsync(approvalId, actor, reason, cancelWorkflow, cancellationToken);

    /// <summary>Adds a comment to an approval.</summary>
    /// <param name="approvalId">The approval id.</param>
    /// <param name="author">The comment author.</param>
    /// <param name="text">The comment text.</param>
    /// <returns>The added comment, or <see langword="null"/> when the approval is unknown.</returns>
    public Task<ApprovalComment?> AddCommentAsync(Guid approvalId, string author, string text) =>
        Task.FromResult(_runtime.AddComment(approvalId, author, text));

    /// <summary>Runs the due-work pass (reminders, escalations, expiries) over open approvals.</summary>
    /// <param name="cancellationToken">A token to cancel the pass.</param>
    /// <returns>A summary of what the pass did.</returns>
    public Task<ApprovalDueWorkSummary> RunDueAsync(CancellationToken cancellationToken = default) =>
        _runtime.RunDueAsync(cancellationToken);

    /// <summary>Gets an approval by id.</summary>
    /// <param name="approvalId">The approval id.</param>
    /// <returns>The approval, or <see langword="null"/> when not found.</returns>
    public ApprovalInstance? GetApproval(Guid approvalId) => _store.Get(approvalId);

    /// <summary>Lists the approvals with a pending step assigned to a principal.</summary>
    /// <param name="assignee">The assignee.</param>
    /// <returns>The approvals awaiting the assignee.</returns>
    public IReadOnlyCollection<ApprovalInstance> ListByAssignee(string assignee) => _store.ListByAssignee(assignee);

    /// <summary>Gets the history entries of an approval, oldest first.</summary>
    /// <param name="approvalId">The approval id.</param>
    /// <returns>The history entries.</returns>
    public IReadOnlyList<ApprovalHistoryEntry> GetHistory(Guid approvalId) => _history.ByApproval(approvalId);
}
