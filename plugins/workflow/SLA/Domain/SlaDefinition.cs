namespace FactoryOS.Plugins.Workflow.SLA.Domain;

/// <summary>
/// The reusable blueprint of a service-level agreement: the kind of work it tracks, the policy that decides how
/// its clock runs, the deadline, the optional stages, the reminders that warn before the deadline, the
/// escalations that fire after it, the optional hard timeout, and the permission grants. A definition is data —
/// the runtime turns it, plus a target and a context, into a running SLA instance.
/// Build one with <see cref="Create"/> and the fluent builder.
/// </summary>
public sealed class SlaDefinition
{
    /// <summary>Initializes a new instance of the <see cref="SlaDefinition"/> class.</summary>
    /// <param name="key">The definition key.</param>
    /// <param name="name">The human-readable name.</param>
    /// <param name="targetKind">The kind of work it tracks.</param>
    /// <param name="policy">The policy that decides how the clock runs.</param>
    /// <param name="deadline">The overall business-time budget.</param>
    /// <param name="stages">The ordered stages, when the SLA is staged.</param>
    /// <param name="reminders">The reminders that fire before the deadline.</param>
    /// <param name="escalations">The escalations that fire after the deadline.</param>
    /// <param name="timeout">The hard timeout, if any.</param>
    /// <param name="grants">The permission grants.</param>
    internal SlaDefinition(
        string key,
        string name,
        SlaTargetKind targetKind,
        SlaPolicy policy,
        SlaDeadline deadline,
        IReadOnlyList<SlaStage> stages,
        IReadOnlyList<SlaReminder> reminders,
        IReadOnlyList<SlaEscalation> escalations,
        SlaTimeout? timeout,
        IReadOnlyList<SlaPermissionGrant> grants)
    {
        Key = key;
        Name = name;
        TargetKind = targetKind;
        Policy = policy;
        Deadline = deadline;
        Stages = stages;
        Reminders = reminders;
        Escalations = escalations;
        Timeout = timeout;
        Grants = grants;
    }

    /// <summary>Gets the definition key.</summary>
    public string Key { get; }

    /// <summary>Gets the human-readable name.</summary>
    public string Name { get; }

    /// <summary>Gets the kind of work the SLA tracks.</summary>
    public SlaTargetKind TargetKind { get; }

    /// <summary>Gets the policy that decides how the clock runs.</summary>
    public SlaPolicy Policy { get; }

    /// <summary>Gets the overall business-time budget.</summary>
    public SlaDeadline Deadline { get; }

    /// <summary>Gets the ordered stages; empty when the SLA is not staged.</summary>
    public IReadOnlyList<SlaStage> Stages { get; }

    /// <summary>Gets the reminders that fire before the deadline.</summary>
    public IReadOnlyList<SlaReminder> Reminders { get; }

    /// <summary>Gets the escalations that fire after the deadline.</summary>
    public IReadOnlyList<SlaEscalation> Escalations { get; }

    /// <summary>Gets the hard timeout, if any.</summary>
    public SlaTimeout? Timeout { get; }

    /// <summary>Gets the permission grants.</summary>
    public IReadOnlyList<SlaPermissionGrant> Grants { get; }

    /// <summary>Gets a value indicating whether the SLA runs through stages.</summary>
    public bool IsStaged => Stages.Count > 0;

    /// <summary>Starts building an SLA definition.</summary>
    /// <param name="key">The definition key.</param>
    /// <param name="name">The human-readable name.</param>
    /// <returns>A fluent builder.</returns>
    public static SlaDefinitionBuilder Create(string key, string name) => new(key, name);
}

/// <summary>A fluent builder for <see cref="SlaDefinition"/>.</summary>
public sealed class SlaDefinitionBuilder
{
    private readonly string _key;
    private readonly string _name;
    private readonly List<SlaStage> _stages = [];
    private readonly List<SlaReminder> _reminders = [];
    private readonly List<SlaEscalation> _escalations = [];
    private readonly List<SlaPermissionGrant> _grants = [];
    private SlaTargetKind _targetKind = SlaTargetKind.WorkflowActivity;
    private SlaPolicy _policy = SlaPolicy.TwentyFourSeven;
    private SlaDeadline? _deadline;
    private SlaTimeout? _timeout;

    /// <summary>Initializes a new instance of the <see cref="SlaDefinitionBuilder"/> class.</summary>
    /// <param name="key">The definition key.</param>
    /// <param name="name">The human-readable name.</param>
    public SlaDefinitionBuilder(string key, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _key = key;
        _name = name;
    }

    /// <summary>Sets the kind of work the SLA tracks.</summary>
    /// <param name="targetKind">The target kind.</param>
    /// <returns>The same builder.</returns>
    public SlaDefinitionBuilder For(SlaTargetKind targetKind)
    {
        _targetKind = targetKind;
        return this;
    }

    /// <summary>Sets the policy that decides how the clock runs.</summary>
    /// <param name="policy">The policy.</param>
    /// <returns>The same builder.</returns>
    public SlaDefinitionBuilder Using(SlaPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        _policy = policy;
        return this;
    }

    /// <summary>Sets the overall business-time budget.</summary>
    /// <param name="duration">The budget.</param>
    /// <returns>The same builder.</returns>
    public SlaDefinitionBuilder WithDeadline(TimeSpan duration)
    {
        _deadline = SlaDeadline.In(duration);
        return this;
    }

    /// <summary>Adds a stage with its own business-time budget; stages run in the order they are added.</summary>
    /// <param name="key">The stage key.</param>
    /// <param name="name">The stage name.</param>
    /// <param name="duration">The stage's budget.</param>
    /// <returns>The same builder.</returns>
    public SlaDefinitionBuilder AddStage(string key, string name, TimeSpan duration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "A stage must have a positive budget.");
        }

        _stages.Add(new SlaStage(key, name, duration, _stages.Count + 1));
        return this;
    }

    /// <summary>Adds a reminder that fires the given business time before the deadline.</summary>
    /// <param name="before">How far ahead of the deadline it fires.</param>
    /// <returns>The same builder.</returns>
    public SlaDefinitionBuilder AddReminder(TimeSpan before)
    {
        _reminders.Add(new SlaReminder(before));
        return this;
    }

    /// <summary>Adds an escalation that fires the given business time after the deadline.</summary>
    /// <param name="after">How long after the deadline it fires.</param>
    /// <param name="assignee">Who the work escalates to.</param>
    /// <returns>The same builder.</returns>
    public SlaDefinitionBuilder AddEscalation(TimeSpan after, string assignee)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assignee);
        _escalations.Add(new SlaEscalation(after, assignee, _escalations.Count + 1));
        return this;
    }

    /// <summary>Sets the hard timeout, measured in business time after the deadline.</summary>
    /// <param name="after">How long after the deadline the SLA times out.</param>
    /// <returns>The same builder.</returns>
    public SlaDefinitionBuilder WithTimeout(TimeSpan after)
    {
        if (after <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(after), after, "A timeout must be a positive duration.");
        }

        _timeout = new SlaTimeout(after);
        return this;
    }

    /// <summary>Grants a principal rights over the SLAs this definition produces.</summary>
    /// <param name="principal">The principal.</param>
    /// <param name="permission">The rights.</param>
    /// <returns>The same builder.</returns>
    public SlaDefinitionBuilder Grant(string principal, SlaPermission permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(principal);
        _grants.Add(new SlaPermissionGrant(principal, permission));
        return this;
    }

    /// <summary>Builds the definition.</summary>
    /// <returns>The built <see cref="SlaDefinition"/>.</returns>
    public SlaDefinition Build()
    {
        // A staged SLA's overall budget defaults to the sum of its stages, so the two can never disagree.
        var deadline = _deadline
            ?? (_stages.Count > 0
                ? new SlaDeadline(_stages.Aggregate(TimeSpan.Zero, (total, stage) => total + stage.Duration))
                : throw new InvalidOperationException($"SLA definition '{_key}' needs a deadline or at least one stage."));

        return new SlaDefinition(
            _key,
            _name,
            _targetKind,
            _policy,
            deadline,
            _stages,
            _reminders.OrderByDescending(reminder => reminder.Before).ToArray(),
            _escalations.OrderBy(escalation => escalation.After).ToArray(),
            _timeout,
            _grants);
    }
}
