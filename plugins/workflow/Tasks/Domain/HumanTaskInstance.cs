namespace FactoryOS.Plugins.Workflow.Tasks.Domain;

/// <summary>The live state of one scheduled reminder on a task.</summary>
public sealed class HumanTaskReminderState
{
    internal HumanTaskReminderState(DateTimeOffset fireAtUtc) => FireAtUtc = fireAtUtc;

    /// <summary>Gets the instant the reminder fires.</summary>
    public DateTimeOffset FireAtUtc { get; }

    /// <summary>Gets a value indicating whether the reminder has already fired.</summary>
    public bool Fired { get; private set; }

    internal void MarkFired() => Fired = true;

    internal bool IsDue(DateTimeOffset now) => !Fired && FireAtUtc <= now;
}

/// <summary>The live state of one escalation policy on a task.</summary>
public sealed class HumanTaskEscalationState
{
    internal HumanTaskEscalationState(DateTimeOffset dueAtUtc, HumanTaskAssignment to)
    {
        DueAtUtc = dueAtUtc;
        To = to;
    }

    /// <summary>Gets the instant the escalation becomes due.</summary>
    public DateTimeOffset DueAtUtc { get; }

    /// <summary>Gets the assignment the task escalates to.</summary>
    public HumanTaskAssignment To { get; }

    /// <summary>Gets a value indicating whether the escalation has already been applied.</summary>
    public bool Escalated { get; private set; }

    internal void MarkEscalated() => Escalated = true;

    internal bool IsDue(DateTimeOffset now) => !Escalated && DueAtUtc <= now;
}

/// <summary>
/// A running (or finished) human task: the live state of one instance of a task definition. It carries the
/// current status, the resolved assignee and candidate pool, comments, attachments, audit history, decision,
/// deadline, reminder and escalation state, and — when created from a workflow activity — the link back to the
/// workflow instance and node it completes. The tenant is fixed at creation, so nothing crosses tenants.
/// </summary>
public sealed class HumanTaskInstance
{
    private readonly List<string> _candidates = [];
    private readonly List<HumanTaskComment> _comments = [];
    private readonly List<HumanTaskAttachment> _attachments = [];
    private readonly List<HumanTaskReminderState> _reminders = [];
    private readonly List<HumanTaskEscalationState> _escalations = [];

    private HumanTaskInstance(
        Guid id, string definitionKey, string tenant, string title, HumanTaskCategory category, HumanTaskPriority priority)
    {
        Id = id;
        DefinitionKey = definitionKey;
        Tenant = tenant;
        Title = title;
        Category = category;
        Priority = priority;
        Status = HumanTaskStatus.Created;
        History = new HumanTaskHistory();
    }

    /// <summary>Gets the task identifier.</summary>
    public Guid Id { get; }

    /// <summary>Gets the definition key this task runs.</summary>
    public string DefinitionKey { get; }

    /// <summary>Gets the owning tenant.</summary>
    public string Tenant { get; }

    /// <summary>Gets the display title.</summary>
    public string Title { get; }

    /// <summary>Gets the category.</summary>
    public HumanTaskCategory Category { get; }

    /// <summary>Gets the current priority (may rise on escalation).</summary>
    public HumanTaskPriority Priority { get; private set; }

    /// <summary>Gets the current status.</summary>
    public HumanTaskStatus Status { get; private set; }

    /// <summary>Gets the resolved assignee, if any.</summary>
    public string? Assignee { get; private set; }

    /// <summary>Gets the candidate pool the assignee was (or will be) chosen from.</summary>
    public IReadOnlyList<string> Candidates => _candidates;

    /// <summary>Gets the comments left on the task.</summary>
    public IReadOnlyList<HumanTaskComment> Comments => _comments;

    /// <summary>Gets the attachment references on the task.</summary>
    public IReadOnlyList<HumanTaskAttachment> Attachments => _attachments;

    /// <summary>Gets the audit history.</summary>
    public HumanTaskHistory History { get; }

    /// <summary>Gets the recorded decision, when completed or rejected.</summary>
    public HumanTaskDecision? Decision { get; private set; }

    /// <summary>Gets the resolved deadline, if any.</summary>
    public DateTimeOffset? DeadlineUtc { get; private set; }

    /// <summary>Gets how many times the task has escalated.</summary>
    public int EscalationLevel { get; private set; }

    /// <summary>
    /// Gets the opaque metadata carried from the definition for the orchestration layer to interpret. The task
    /// engine never reads it.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; private set; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets the linked workflow instance id, when created from a workflow activity.</summary>
    public Guid? WorkflowInstanceId { get; private set; }

    /// <summary>Gets the linked workflow activity node id, when created from a workflow activity.</summary>
    public string? WorkflowActivityNodeId { get; private set; }

    /// <summary>Gets the scheduled reminders.</summary>
    public IReadOnlyList<HumanTaskReminderState> Reminders => _reminders;

    /// <summary>Gets the scheduled escalations.</summary>
    public IReadOnlyList<HumanTaskEscalationState> Escalations => _escalations;

    /// <summary>Gets a value indicating whether the task has reached a terminal status.</summary>
    public bool IsFinished => Status is HumanTaskStatus.Completed
        or HumanTaskStatus.Rejected or HumanTaskStatus.Cancelled or HumanTaskStatus.Expired;

    /// <summary>Gets a value indicating whether the task is bound to a workflow activity.</summary>
    public bool IsWorkflowBound => WorkflowInstanceId is not null && WorkflowActivityNodeId is not null;

    /// <summary>Creates a new task instance.</summary>
    /// <param name="id">The task id.</param>
    /// <param name="definitionKey">The definition key.</param>
    /// <param name="tenant">The owning tenant.</param>
    /// <param name="title">The display title.</param>
    /// <param name="category">The category.</param>
    /// <param name="priority">The priority.</param>
    /// <returns>The new instance.</returns>
    public static HumanTaskInstance Create(
        Guid id,
        string definitionKey,
        string tenant,
        string title,
        HumanTaskCategory category,
        HumanTaskPriority priority)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        return new HumanTaskInstance(id, definitionKey, tenant, title, category, priority);
    }

    /// <summary>Links the task to a workflow activity it will complete.</summary>
    /// <param name="workflowInstanceId">The workflow instance id.</param>
    /// <param name="activityNodeId">The workflow activity node id.</param>
    public void BindToWorkflow(Guid workflowInstanceId, string activityNodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activityNodeId);
        WorkflowInstanceId = workflowInstanceId;
        WorkflowActivityNodeId = activityNodeId;
    }

    /// <summary>Sets the opaque orchestration metadata carried from the definition.</summary>
    /// <param name="metadata">The metadata.</param>
    public void SetMetadata(IReadOnlyDictionary<string, string> metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        Metadata = metadata;
    }

    /// <summary>Records the candidate pool.</summary>
    /// <param name="candidates">The candidates.</param>
    public void SetCandidates(IEnumerable<string> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        _candidates.Clear();
        _candidates.AddRange(candidates);
    }

    /// <summary>Sets the resolved deadline.</summary>
    /// <param name="deadlineUtc">The deadline.</param>
    public void SetDeadline(DateTimeOffset deadlineUtc) => DeadlineUtc = deadlineUtc;

    /// <summary>Adds a scheduled reminder.</summary>
    /// <param name="fireAtUtc">When the reminder fires.</param>
    public void AddReminder(DateTimeOffset fireAtUtc) => _reminders.Add(new HumanTaskReminderState(fireAtUtc));

    /// <summary>Adds a scheduled escalation.</summary>
    /// <param name="dueAtUtc">When the escalation is due.</param>
    /// <param name="to">Who to escalate to.</param>
    public void AddEscalation(DateTimeOffset dueAtUtc, HumanTaskAssignment to)
    {
        ArgumentNullException.ThrowIfNull(to);
        _escalations.Add(new HumanTaskEscalationState(dueAtUtc, to));
    }

    /// <summary>Assigns the task and moves it into the waiting state.</summary>
    /// <param name="assignee">The resolved assignee.</param>
    public void AssignTo(string? assignee)
    {
        Assignee = assignee;
        Status = HumanTaskStatus.Waiting;
    }

    /// <summary>Moves the task to in-progress when the assignee opens it.</summary>
    public void MarkInProgress()
    {
        if (Status is HumanTaskStatus.Waiting or HumanTaskStatus.Assigned or HumanTaskStatus.Escalated)
        {
            Status = HumanTaskStatus.InProgress;
        }
    }

    /// <summary>Completes the task with a decision.</summary>
    /// <param name="decision">The decision.</param>
    public void Complete(HumanTaskDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        Decision = decision;
        Status = HumanTaskStatus.Completed;
    }

    /// <summary>Rejects the task with a decision.</summary>
    /// <param name="decision">The decision.</param>
    public void Reject(HumanTaskDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        Decision = decision;
        Status = HumanTaskStatus.Rejected;
    }

    /// <summary>Cancels the task.</summary>
    public void Cancel() => Status = HumanTaskStatus.Cancelled;

    /// <summary>Expires the task.</summary>
    public void Expire() => Status = HumanTaskStatus.Expired;

    /// <summary>Reassigns the task to a new owner without changing its escalation level.</summary>
    /// <param name="assignee">The new assignee.</param>
    public void Reassign(string? assignee)
    {
        Assignee = assignee;
        if (!IsFinished)
        {
            Status = HumanTaskStatus.Waiting;
        }
    }

    /// <summary>Escalates the task to a new owner, raising its escalation level and priority.</summary>
    /// <param name="assignee">The new assignee.</param>
    public void Escalate(string? assignee)
    {
        Assignee = assignee;
        EscalationLevel++;
        Status = HumanTaskStatus.Escalated;
        if (Priority < HumanTaskPriority.Critical)
        {
            Priority++;
        }
    }

    /// <summary>Adds a comment.</summary>
    /// <param name="comment">The comment.</param>
    public void AddComment(HumanTaskComment comment)
    {
        ArgumentNullException.ThrowIfNull(comment);
        _comments.Add(comment);
    }

    /// <summary>Adds an attachment reference.</summary>
    /// <param name="attachment">The attachment.</param>
    public void AddAttachment(HumanTaskAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        _attachments.Add(attachment);
    }

    /// <summary>Lists reminders due at or before an instant that have not yet fired.</summary>
    /// <param name="now">The current instant.</param>
    /// <returns>The due reminders.</returns>
    public IReadOnlyList<HumanTaskReminderState> DueReminders(DateTimeOffset now) =>
        _reminders.Where(reminder => reminder.IsDue(now)).ToArray();

    /// <summary>Lists escalations due at or before an instant that have not yet been applied.</summary>
    /// <param name="now">The current instant.</param>
    /// <returns>The due escalations.</returns>
    public IReadOnlyList<HumanTaskEscalationState> DueEscalations(DateTimeOffset now) =>
        _escalations.Where(escalation => escalation.IsDue(now)).ToArray();

    /// <summary>Marks a reminder as fired.</summary>
    /// <param name="reminder">The reminder.</param>
    public void MarkReminderFired(HumanTaskReminderState reminder)
    {
        ArgumentNullException.ThrowIfNull(reminder);
        reminder.MarkFired();
    }

    /// <summary>Marks an escalation as applied.</summary>
    /// <param name="escalation">The escalation.</param>
    public void MarkEscalationApplied(HumanTaskEscalationState escalation)
    {
        ArgumentNullException.ThrowIfNull(escalation);
        escalation.MarkEscalated();
    }
}
