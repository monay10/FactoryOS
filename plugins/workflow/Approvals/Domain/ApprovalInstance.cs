namespace FactoryOS.Plugins.Workflow.Approvals.Domain;

/// <summary>The live state of one scheduled reminder on an approval.</summary>
public sealed class ApprovalReminderState
{
    internal ApprovalReminderState(DateTimeOffset fireAtUtc) => FireAtUtc = fireAtUtc;

    /// <summary>Gets the instant the reminder fires.</summary>
    public DateTimeOffset FireAtUtc { get; }

    /// <summary>Gets a value indicating whether the reminder has already fired.</summary>
    public bool Fired { get; private set; }

    internal void MarkFired() => Fired = true;

    internal bool IsDue(DateTimeOffset now) => !Fired && FireAtUtc <= now;
}

/// <summary>The live state of one escalation policy on an approval.</summary>
public sealed class ApprovalEscalationState
{
    internal ApprovalEscalationState(DateTimeOffset dueAtUtc, ApprovalAssignment to)
    {
        DueAtUtc = dueAtUtc;
        To = to;
    }

    /// <summary>Gets the instant the escalation becomes due.</summary>
    public DateTimeOffset DueAtUtc { get; }

    /// <summary>Gets the assignment the pending steps escalate to.</summary>
    public ApprovalAssignment To { get; }

    /// <summary>Gets a value indicating whether the escalation has already been applied.</summary>
    public bool Escalated { get; private set; }

    internal void MarkEscalated() => Escalated = true;

    internal bool IsDue(DateTimeOffset now) => !Escalated && DueAtUtc <= now;
}

/// <summary>
/// A running (or finished) approval: the live state of one instance of an approval definition. It carries the
/// status and outcome, the current stage level, the per-participant steps, the context values (used to resolve
/// later stages and rules), comments, audit history, deadline and reminder / escalation state, and — when
/// created from a workflow activity — the link back to the workflow instance and node it completes. The tenant
/// is fixed at creation.
/// </summary>
public sealed class ApprovalInstance
{
    private readonly List<ApprovalStep> _steps = [];
    private readonly List<ApprovalComment> _comments = [];
    private readonly List<ApprovalReminderState> _reminders = [];
    private readonly List<ApprovalEscalationState> _escalations = [];
    private readonly Dictionary<string, object?> _values;

    private ApprovalInstance(
        Guid id, string definitionKey, string tenant, string title, IReadOnlyDictionary<string, object?> values)
    {
        Id = id;
        DefinitionKey = definitionKey;
        Tenant = tenant;
        Title = title;
        Status = ApprovalStatus.Created;
        Outcome = ApprovalOutcome.Pending;
        CurrentLevel = ApprovalLevel.First;
        History = new ApprovalHistory();
        _values = new Dictionary<string, object?>(values, StringComparer.Ordinal);
    }

    /// <summary>Gets the approval identifier.</summary>
    public Guid Id { get; }

    /// <summary>Gets the definition key this approval runs.</summary>
    public string DefinitionKey { get; }

    /// <summary>Gets the owning tenant.</summary>
    public string Tenant { get; }

    /// <summary>Gets the display title.</summary>
    public string Title { get; }

    /// <summary>Gets the current status.</summary>
    public ApprovalStatus Status { get; private set; }

    /// <summary>Gets the decision outcome.</summary>
    public ApprovalOutcome Outcome { get; private set; }

    /// <summary>Gets the level of the currently active stage.</summary>
    public ApprovalLevel CurrentLevel { get; private set; }

    /// <summary>Gets a value indicating whether the approval has been escalated at least once.</summary>
    public bool HasEscalated { get; private set; }

    /// <summary>Gets the per-participant steps.</summary>
    public IReadOnlyList<ApprovalStep> Steps => _steps;

    /// <summary>Gets the comments left on the approval.</summary>
    public IReadOnlyList<ApprovalComment> Comments => _comments;

    /// <summary>Gets the audit history.</summary>
    public ApprovalHistory History { get; }

    /// <summary>Gets the context values (for resolving later stages and rules).</summary>
    public IReadOnlyDictionary<string, object?> Values => _values;

    /// <summary>Gets the resolved deadline, if any.</summary>
    public DateTimeOffset? DeadlineUtc { get; private set; }

    /// <summary>Gets the linked workflow instance id, when created from a workflow activity.</summary>
    public Guid? WorkflowInstanceId { get; private set; }

    /// <summary>Gets the linked workflow activity node id, when created from a workflow activity.</summary>
    public string? WorkflowActivityNodeId { get; private set; }

    /// <summary>Gets the scheduled reminders.</summary>
    public IReadOnlyList<ApprovalReminderState> Reminders => _reminders;

    /// <summary>Gets the scheduled escalations.</summary>
    public IReadOnlyList<ApprovalEscalationState> Escalations => _escalations;

    /// <summary>Gets a value indicating whether the approval has reached a terminal status.</summary>
    public bool IsFinished => Status is ApprovalStatus.Approved
        or ApprovalStatus.Rejected or ApprovalStatus.Cancelled or ApprovalStatus.Expired;

    /// <summary>Gets a value indicating whether the approval is bound to a workflow activity.</summary>
    public bool IsWorkflowBound => WorkflowInstanceId is not null && WorkflowActivityNodeId is not null;

    /// <summary>Gets the steps of the currently active stage.</summary>
    public IReadOnlyList<ApprovalStep> ActiveStageSteps =>
        _steps.Where(step => step.Level == CurrentLevel).ToArray();

    /// <summary>Creates a new approval instance.</summary>
    /// <param name="id">The approval id.</param>
    /// <param name="definitionKey">The definition key.</param>
    /// <param name="tenant">The owning tenant.</param>
    /// <param name="title">The display title.</param>
    /// <param name="values">The context values.</param>
    /// <returns>The new instance.</returns>
    public static ApprovalInstance Create(
        Guid id, string definitionKey, string tenant, string title, IReadOnlyDictionary<string, object?> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(values);
        return new ApprovalInstance(id, definitionKey, tenant, title, values);
    }

    /// <summary>Links the approval to a workflow activity it will complete.</summary>
    /// <param name="workflowInstanceId">The workflow instance id.</param>
    /// <param name="activityNodeId">The workflow activity node id.</param>
    public void BindToWorkflow(Guid workflowInstanceId, string activityNodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activityNodeId);
        WorkflowInstanceId = workflowInstanceId;
        WorkflowActivityNodeId = activityNodeId;
    }

    /// <summary>Marks the approval as started.</summary>
    public void MarkStarted() => Status = ApprovalStatus.InProgress;

    /// <summary>Activates a stage, adding its resolved steps and making it the current level.</summary>
    /// <param name="level">The stage level.</param>
    /// <param name="steps">The resolved steps.</param>
    public void ActivateStage(ApprovalLevel level, IEnumerable<ApprovalStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        CurrentLevel = level;
        _steps.AddRange(steps);
    }

    /// <summary>Records a participant's vote on their active step.</summary>
    /// <param name="decision">The decision.</param>
    /// <returns>The step the vote was recorded on.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no matching pending step is active.</exception>
    public ApprovalStep RecordDecision(ApprovalDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var step = _steps.FirstOrDefault(candidate =>
            candidate.Level == CurrentLevel
            && string.Equals(candidate.ParticipantId, decision.ParticipantId, StringComparison.Ordinal)
            && candidate.Status == ApprovalParticipantStatus.Pending)
            ?? throw new InvalidOperationException(
                $"Participant '{decision.ParticipantId}' has no pending step in the active stage of approval '{Id}'.");
        step.Record(decision);
        return step;
    }

    /// <summary>
    /// Escalates by reassigning every pending step of the active stage to a new assignee. An escalated approval
    /// is handed to a new owner and no longer auto-expires — it waits for that owner to act.
    /// </summary>
    /// <param name="assignee">The new assignee.</param>
    public void EscalatePending(string assignee)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assignee);
        foreach (var step in _steps.Where(step =>
            step.Level == CurrentLevel && step.Status == ApprovalParticipantStatus.Pending))
        {
            step.Reassign(assignee);
        }

        HasEscalated = true;
    }

    /// <summary>Finishes the approval with an outcome, skipping any pending steps.</summary>
    /// <param name="outcome">The terminal outcome.</param>
    public void Finish(ApprovalOutcome outcome)
    {
        foreach (var step in _steps.Where(step => step.Status == ApprovalParticipantStatus.Pending))
        {
            step.Skip();
        }

        Outcome = outcome;
        Status = outcome == ApprovalOutcome.Approved ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
    }

    /// <summary>Cancels the approval.</summary>
    public void Cancel()
    {
        foreach (var step in _steps.Where(step => step.Status == ApprovalParticipantStatus.Pending))
        {
            step.Skip();
        }

        Status = ApprovalStatus.Cancelled;
    }

    /// <summary>Expires the approval.</summary>
    public void Expire()
    {
        foreach (var step in _steps.Where(step => step.Status == ApprovalParticipantStatus.Pending))
        {
            step.Skip();
        }

        Status = ApprovalStatus.Expired;
    }

    /// <summary>Adds a comment.</summary>
    /// <param name="comment">The comment.</param>
    public void AddComment(ApprovalComment comment)
    {
        ArgumentNullException.ThrowIfNull(comment);
        _comments.Add(comment);
    }

    /// <summary>Sets the resolved deadline.</summary>
    /// <param name="deadlineUtc">The deadline.</param>
    public void SetDeadline(DateTimeOffset deadlineUtc) => DeadlineUtc = deadlineUtc;

    /// <summary>Adds a scheduled reminder.</summary>
    /// <param name="fireAtUtc">When the reminder fires.</param>
    public void AddReminder(DateTimeOffset fireAtUtc) => _reminders.Add(new ApprovalReminderState(fireAtUtc));

    /// <summary>Adds a scheduled escalation.</summary>
    /// <param name="dueAtUtc">When the escalation is due.</param>
    /// <param name="to">Who to escalate to.</param>
    public void AddEscalation(DateTimeOffset dueAtUtc, ApprovalAssignment to)
    {
        ArgumentNullException.ThrowIfNull(to);
        _escalations.Add(new ApprovalEscalationState(dueAtUtc, to));
    }

    /// <summary>Lists reminders due at or before an instant that have not yet fired.</summary>
    /// <param name="now">The current instant.</param>
    /// <returns>The due reminders.</returns>
    public IReadOnlyList<ApprovalReminderState> DueReminders(DateTimeOffset now) =>
        _reminders.Where(reminder => reminder.IsDue(now)).ToArray();

    /// <summary>Lists escalations due at or before an instant that have not yet been applied.</summary>
    /// <param name="now">The current instant.</param>
    /// <returns>The due escalations.</returns>
    public IReadOnlyList<ApprovalEscalationState> DueEscalations(DateTimeOffset now) =>
        _escalations.Where(escalation => escalation.IsDue(now)).ToArray();

    /// <summary>Marks a reminder as fired.</summary>
    /// <param name="reminder">The reminder.</param>
    public void MarkReminderFired(ApprovalReminderState reminder)
    {
        ArgumentNullException.ThrowIfNull(reminder);
        reminder.MarkFired();
    }

    /// <summary>Marks an escalation as applied.</summary>
    /// <param name="escalation">The escalation.</param>
    public void MarkEscalationApplied(ApprovalEscalationState escalation)
    {
        ArgumentNullException.ThrowIfNull(escalation);
        escalation.MarkEscalated();
    }
}
