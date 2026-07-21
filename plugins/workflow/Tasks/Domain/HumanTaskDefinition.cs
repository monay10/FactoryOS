namespace FactoryOS.Plugins.Workflow.Tasks.Domain;

/// <summary>
/// The design-time template of a human task: its identity, category and priority, how it is assigned, who may
/// act on it, its deadline / reminder / escalation policy, and the optional form it presents and the optional
/// workflow activity it satisfies. Instances are created from a definition when a workflow reaches a task.
/// </summary>
public sealed class HumanTaskDefinition
{
    internal HumanTaskDefinition(
        string key,
        string name,
        string title,
        HumanTaskCategory category,
        HumanTaskPriority priority,
        HumanTaskAssignment assignment,
        IReadOnlyList<HumanTaskPermissionGrant> permissions,
        HumanTaskDeadline? deadline,
        IReadOnlyList<HumanTaskReminder> reminders,
        IReadOnlyList<HumanTaskEscalation> escalations,
        IReadOnlyDictionary<string, string> metadata,
        string? activityKey)
    {
        Key = key;
        Name = name;
        Title = title;
        Category = category;
        Priority = priority;
        Assignment = assignment;
        Permissions = permissions;
        Deadline = deadline;
        Reminders = reminders;
        Escalations = escalations;
        Metadata = metadata;
        ActivityKey = activityKey;
    }

    /// <summary>Gets the task key.</summary>
    public string Key { get; }

    /// <summary>Gets the display name.</summary>
    public string Name { get; }

    /// <summary>Gets the display title shown to the assignee.</summary>
    public string Title { get; }

    /// <summary>Gets the category.</summary>
    public HumanTaskCategory Category { get; }

    /// <summary>Gets the priority.</summary>
    public HumanTaskPriority Priority { get; }

    /// <summary>Gets the assignment strategy.</summary>
    public HumanTaskAssignment Assignment { get; }

    /// <summary>Gets the permission grants.</summary>
    public IReadOnlyList<HumanTaskPermissionGrant> Permissions { get; }

    /// <summary>Gets the deadline policy, if any.</summary>
    public HumanTaskDeadline? Deadline { get; }

    /// <summary>Gets the reminder policies.</summary>
    public IReadOnlyList<HumanTaskReminder> Reminders { get; }

    /// <summary>Gets the escalation policies.</summary>
    public IReadOnlyList<HumanTaskEscalation> Escalations { get; }

    /// <summary>
    /// Gets opaque metadata carried with the task for the orchestration layer to interpret. The task engine
    /// never reads it — it knows nothing of forms, notifications or inboxes; a subscriber to the task's events
    /// decides what a given key means (for example a <c>"form"</c> entry naming a form to open).
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }

    /// <summary>Gets the workflow activity key this task satisfies, if any.</summary>
    public string? ActivityKey { get; }

    /// <summary>Begins building a human task definition.</summary>
    /// <param name="key">The task key.</param>
    /// <param name="name">The display name.</param>
    /// <param name="assignment">The assignment strategy.</param>
    /// <returns>A builder.</returns>
    public static HumanTaskDefinitionBuilder Create(string key, string name, HumanTaskAssignment assignment) =>
        new(key, name, assignment);
}

/// <summary>A fluent builder for a <see cref="HumanTaskDefinition"/>.</summary>
public sealed class HumanTaskDefinitionBuilder
{
    private readonly string _key;
    private readonly string _name;
    private readonly HumanTaskAssignment _assignment;
    private readonly List<HumanTaskPermissionGrant> _permissions = [];
    private readonly List<HumanTaskReminder> _reminders = [];
    private readonly List<HumanTaskEscalation> _escalations = [];
    private readonly Dictionary<string, string> _metadata = new(StringComparer.Ordinal);
    private string? _title;
    private HumanTaskCategory _category = HumanTaskCategory.General;
    private HumanTaskPriority _priority = HumanTaskPriority.Normal;
    private HumanTaskDeadline? _deadline;
    private string? _activityKey;

    /// <summary>Initializes a new instance of the <see cref="HumanTaskDefinitionBuilder"/> class.</summary>
    /// <param name="key">The task key.</param>
    /// <param name="name">The display name.</param>
    /// <param name="assignment">The assignment strategy.</param>
    public HumanTaskDefinitionBuilder(string key, string name, HumanTaskAssignment assignment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(assignment);
        _key = key;
        _name = name;
        _assignment = assignment;
    }

    /// <summary>Sets the title.</summary>
    /// <param name="title">The title.</param>
    /// <returns>The same builder.</returns>
    public HumanTaskDefinitionBuilder WithTitle(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        _title = title;
        return this;
    }

    /// <summary>Sets the category.</summary>
    /// <param name="category">The category.</param>
    /// <returns>The same builder.</returns>
    public HumanTaskDefinitionBuilder OfCategory(HumanTaskCategory category)
    {
        _category = category;
        return this;
    }

    /// <summary>Sets the priority.</summary>
    /// <param name="priority">The priority.</param>
    /// <returns>The same builder.</returns>
    public HumanTaskDefinitionBuilder WithPriority(HumanTaskPriority priority)
    {
        _priority = priority;
        return this;
    }

    /// <summary>Sets the deadline policy.</summary>
    /// <param name="deadline">The deadline.</param>
    /// <returns>The same builder.</returns>
    public HumanTaskDefinitionBuilder WithDeadline(HumanTaskDeadline deadline)
    {
        ArgumentNullException.ThrowIfNull(deadline);
        _deadline = deadline;
        return this;
    }

    /// <summary>Adds a reminder policy.</summary>
    /// <param name="reminder">The reminder.</param>
    /// <returns>The same builder.</returns>
    public HumanTaskDefinitionBuilder AddReminder(HumanTaskReminder reminder)
    {
        ArgumentNullException.ThrowIfNull(reminder);
        _reminders.Add(reminder);
        return this;
    }

    /// <summary>Adds an escalation policy.</summary>
    /// <param name="escalation">The escalation.</param>
    /// <returns>The same builder.</returns>
    public HumanTaskDefinitionBuilder AddEscalation(HumanTaskEscalation escalation)
    {
        ArgumentNullException.ThrowIfNull(escalation);
        _escalations.Add(escalation);
        return this;
    }

    /// <summary>Adds a permission grant.</summary>
    /// <param name="grant">The grant.</param>
    /// <returns>The same builder.</returns>
    public HumanTaskDefinitionBuilder AddPermission(HumanTaskPermissionGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        _permissions.Add(grant);
        return this;
    }

    /// <summary>
    /// Adds an opaque metadata entry for the orchestration layer. The task engine never interprets it; a
    /// subscriber to the task's events decides what the key means (e.g. <c>"form"</c> naming a form to open).
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <returns>The same builder.</returns>
    public HumanTaskDefinitionBuilder WithMetadata(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        _metadata[key] = value;
        return this;
    }

    /// <summary>Binds the workflow activity key the task satisfies on completion.</summary>
    /// <param name="activityKey">The activity key.</param>
    /// <returns>The same builder.</returns>
    public HumanTaskDefinitionBuilder ForActivity(string activityKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activityKey);
        _activityKey = activityKey;
        return this;
    }

    /// <summary>Builds the definition.</summary>
    /// <returns>The built definition.</returns>
    /// <exception cref="InvalidOperationException">Thrown when an escalation is set without a deadline.</exception>
    public HumanTaskDefinition Build()
    {
        if (_escalations.Count > 0 && _deadline is null)
        {
            throw new InvalidOperationException($"Task '{_key}' declares an escalation but has no deadline.");
        }

        if (_reminders.Count > 0 && _deadline is null)
        {
            throw new InvalidOperationException($"Task '{_key}' declares a reminder but has no deadline.");
        }

        return new HumanTaskDefinition(
            _key, _name, _title ?? _name, _category, _priority, _assignment, _permissions,
            _deadline, _reminders, _escalations, _metadata, _activityKey);
    }
}
