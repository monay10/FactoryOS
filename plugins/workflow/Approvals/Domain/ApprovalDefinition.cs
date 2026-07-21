namespace FactoryOS.Plugins.Workflow.Approvals.Domain;

/// <summary>
/// The design-time template of an approval: its identity, its ordered stages (each with participants and a
/// decision policy), any auto-decision rules, its permission grants and deadline / reminder / escalation
/// policy, and the optional workflow activity it satisfies. The overall <see cref="Structure"/> — single,
/// parallel or sequential — is derived from the stages.
/// </summary>
public sealed class ApprovalDefinition
{
    internal ApprovalDefinition(
        string key,
        string name,
        string title,
        IReadOnlyList<ApprovalStage> stages,
        IReadOnlyList<ApprovalRule> rules,
        IReadOnlyList<ApprovalPermissionGrant> permissions,
        ApprovalDeadline? deadline,
        IReadOnlyList<ApprovalReminder> reminders,
        IReadOnlyList<ApprovalEscalation> escalations,
        string? activityKey)
    {
        Key = key;
        Name = name;
        Title = title;
        Stages = stages;
        Rules = rules;
        Permissions = permissions;
        Deadline = deadline;
        Reminders = reminders;
        Escalations = escalations;
        ActivityKey = activityKey;
        Structure = DeriveStructure(stages);
    }

    /// <summary>Gets the approval key.</summary>
    public string Key { get; }

    /// <summary>Gets the display name.</summary>
    public string Name { get; }

    /// <summary>Gets the display title.</summary>
    public string Title { get; }

    /// <summary>Gets the overall structure derived from the stages.</summary>
    public ApprovalStructure Structure { get; }

    /// <summary>Gets the ordered stages.</summary>
    public IReadOnlyList<ApprovalStage> Stages { get; }

    /// <summary>Gets the auto-decision rules, evaluated in order at start.</summary>
    public IReadOnlyList<ApprovalRule> Rules { get; }

    /// <summary>Gets the permission grants.</summary>
    public IReadOnlyList<ApprovalPermissionGrant> Permissions { get; }

    /// <summary>Gets the deadline policy, if any.</summary>
    public ApprovalDeadline? Deadline { get; }

    /// <summary>Gets the reminder policies.</summary>
    public IReadOnlyList<ApprovalReminder> Reminders { get; }

    /// <summary>Gets the escalation policies.</summary>
    public IReadOnlyList<ApprovalEscalation> Escalations { get; }

    /// <summary>Gets the workflow activity key this approval satisfies, if any.</summary>
    public string? ActivityKey { get; }

    /// <summary>Begins building an approval definition.</summary>
    /// <param name="key">The approval key.</param>
    /// <param name="name">The display name.</param>
    /// <returns>A builder.</returns>
    public static ApprovalDefinitionBuilder Create(string key, string name) => new(key, name);

    private static ApprovalStructure DeriveStructure(IReadOnlyList<ApprovalStage> stages)
    {
        if (stages.Count > 1)
        {
            return ApprovalStructure.Sequential;
        }

        var only = stages[0];
        return only.Participants.Count == 1 && only.Policy.Kind == ApprovalPolicyKind.Single
            ? ApprovalStructure.Single
            : ApprovalStructure.Parallel;
    }
}

/// <summary>A fluent builder for an <see cref="ApprovalDefinition"/>.</summary>
public sealed class ApprovalDefinitionBuilder
{
    private readonly string _key;
    private readonly string _name;
    private readonly List<ApprovalStage> _stages = [];
    private readonly List<ApprovalRule> _rules = [];
    private readonly List<ApprovalPermissionGrant> _permissions = [];
    private readonly List<ApprovalReminder> _reminders = [];
    private readonly List<ApprovalEscalation> _escalations = [];
    private string? _title;
    private ApprovalDeadline? _deadline;
    private string? _activityKey;

    /// <summary>Initializes a new instance of the <see cref="ApprovalDefinitionBuilder"/> class.</summary>
    /// <param name="key">The approval key.</param>
    /// <param name="name">The display name.</param>
    public ApprovalDefinitionBuilder(string key, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _key = key;
        _name = name;
    }

    /// <summary>Sets the title.</summary>
    /// <param name="title">The title.</param>
    /// <returns>The same builder.</returns>
    public ApprovalDefinitionBuilder WithTitle(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        _title = title;
        return this;
    }

    /// <summary>Adds a single-approver stage.</summary>
    /// <param name="participantId">The participant id.</param>
    /// <param name="assignment">Who the approver is.</param>
    /// <returns>The same builder.</returns>
    public ApprovalDefinitionBuilder AddSingle(string participantId, ApprovalAssignment assignment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(participantId);
        ArgumentNullException.ThrowIfNull(assignment);
        return AddStage($"stage-{_stages.Count + 1}", ApprovalPolicy.Single, [new ApprovalParticipant(participantId, assignment)]);
    }

    /// <summary>Adds a stage with a decision policy and participants.</summary>
    /// <param name="name">The stage name.</param>
    /// <param name="policy">The decision policy.</param>
    /// <param name="participants">The participants.</param>
    /// <returns>The same builder.</returns>
    public ApprovalDefinitionBuilder AddStage(
        string name, ApprovalPolicy policy, IReadOnlyList<ApprovalParticipant> participants)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(participants);
        _stages.Add(new ApprovalStage(new ApprovalLevel(_stages.Count + 1), name, policy, participants));
        return this;
    }

    /// <summary>Sets the deadline policy.</summary>
    /// <param name="deadline">The deadline.</param>
    /// <returns>The same builder.</returns>
    public ApprovalDefinitionBuilder WithDeadline(ApprovalDeadline deadline)
    {
        ArgumentNullException.ThrowIfNull(deadline);
        _deadline = deadline;
        return this;
    }

    /// <summary>Adds a reminder policy.</summary>
    /// <param name="reminder">The reminder.</param>
    /// <returns>The same builder.</returns>
    public ApprovalDefinitionBuilder AddReminder(ApprovalReminder reminder)
    {
        ArgumentNullException.ThrowIfNull(reminder);
        _reminders.Add(reminder);
        return this;
    }

    /// <summary>Adds an escalation policy.</summary>
    /// <param name="escalation">The escalation.</param>
    /// <returns>The same builder.</returns>
    public ApprovalDefinitionBuilder AddEscalation(ApprovalEscalation escalation)
    {
        ArgumentNullException.ThrowIfNull(escalation);
        _escalations.Add(escalation);
        return this;
    }

    /// <summary>Adds an auto-decision rule.</summary>
    /// <param name="rule">The rule.</param>
    /// <returns>The same builder.</returns>
    public ApprovalDefinitionBuilder AddRule(ApprovalRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _rules.Add(rule);
        return this;
    }

    /// <summary>Adds a permission grant.</summary>
    /// <param name="grant">The grant.</param>
    /// <returns>The same builder.</returns>
    public ApprovalDefinitionBuilder AddPermission(ApprovalPermissionGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        _permissions.Add(grant);
        return this;
    }

    /// <summary>Binds the workflow activity key the approval satisfies on completion.</summary>
    /// <param name="activityKey">The activity key.</param>
    /// <returns>The same builder.</returns>
    public ApprovalDefinitionBuilder ForActivity(string activityKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activityKey);
        _activityKey = activityKey;
        return this;
    }

    /// <summary>Validates and builds the definition.</summary>
    /// <returns>The built definition.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the structure is invalid.</exception>
    public ApprovalDefinition Build()
    {
        if (_stages.Count == 0)
        {
            throw new InvalidOperationException($"Approval '{_key}' must have at least one stage.");
        }

        if ((_reminders.Count > 0 || _escalations.Count > 0) && _deadline is null)
        {
            throw new InvalidOperationException(
                $"Approval '{_key}' declares a reminder or escalation but has no deadline.");
        }

        var participantIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var stage in _stages)
        {
            if (stage.Participants.Count == 0)
            {
                throw new InvalidOperationException($"Stage '{stage.Name}' of approval '{_key}' has no participant.");
            }

            if (stage.Policy.Kind == ApprovalPolicyKind.Single && stage.Participants.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Stage '{stage.Name}' of approval '{_key}' uses the Single policy but has {stage.Participants.Count} participants.");
            }

            foreach (var participant in stage.Participants)
            {
                if (!participantIds.Add(participant.Id))
                {
                    throw new InvalidOperationException(
                        $"Duplicate participant id '{participant.Id}' in approval '{_key}'.");
                }
            }
        }

        return new ApprovalDefinition(
            _key, _name, _title ?? _name, _stages, _rules, _permissions,
            _deadline, _reminders, _escalations, _activityKey);
    }
}
