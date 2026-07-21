namespace FactoryOS.Plugins.Forms.Engine.Domain;

/// <summary>A mutable bag of a form instance's field values, keyed by field key and read by expressions.</summary>
public sealed class FormValues
{
    private readonly Dictionary<string, object?> _values;

    /// <summary>Initializes a new, empty value bag.</summary>
    public FormValues() => _values = new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>Initializes a value bag seeded from an existing map.</summary>
    /// <param name="values">The initial values.</param>
    public FormValues(IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = new Dictionary<string, object?>(values, StringComparer.Ordinal);
    }

    /// <summary>Gets the values as a read-only map for expression evaluation.</summary>
    /// <returns>The read-only values.</returns>
    public IReadOnlyDictionary<string, object?> AsReadOnly() => _values;

    /// <summary>Sets a field value.</summary>
    /// <param name="key">The field key.</param>
    /// <param name="value">The value.</param>
    public void Set(string key, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _values[key] = value;
    }

    /// <summary>Gets a field value, or <see langword="null"/> when unset.</summary>
    /// <param name="key">The field key.</param>
    /// <returns>The value.</returns>
    public object? Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return _values.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>Determines whether a field has a value.</summary>
    /// <param name="key">The field key.</param>
    /// <returns><see langword="true"/> when present.</returns>
    public bool Has(string key) => _values.ContainsKey(key);

    /// <summary>Copies a set of values into the bag, overwriting existing keys.</summary>
    /// <param name="values">The values to merge in.</param>
    public void Merge(IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        foreach (var pair in values)
        {
            _values[pair.Key] = pair.Value;
        }
    }
}

/// <summary>
/// A running (or finished) filling of a form: the live state of one form instance. It carries the field
/// values, the audit history, the resolved assignee, and — when opened from a workflow activity — the link
/// back to the workflow instance and node it will complete on submission. The tenant is fixed at creation, so
/// no code path reads or writes across tenants.
/// </summary>
public sealed class FormInstance
{
    private FormInstance(Guid id, string formKey, FormVersion version, string tenant, FormValues values)
    {
        Id = id;
        FormKey = formKey;
        Version = version;
        Tenant = tenant;
        Values = values;
        State = FormInstanceState.Open;
        History = new FormHistory();
    }

    /// <summary>Gets the instance identifier.</summary>
    public Guid Id { get; }

    /// <summary>Gets the form key this instance fills.</summary>
    public string FormKey { get; }

    /// <summary>Gets the form version this instance fills.</summary>
    public FormVersion Version { get; }

    /// <summary>Gets the owning tenant.</summary>
    public string Tenant { get; }

    /// <summary>Gets the field values.</summary>
    public FormValues Values { get; }

    /// <summary>Gets the current state.</summary>
    public FormInstanceState State { get; private set; }

    /// <summary>Gets the resolved assignee, if any.</summary>
    public string? Assignee { get; private set; }

    /// <summary>Gets who submitted the form, if it was submitted.</summary>
    public string? SubmittedBy { get; private set; }

    /// <summary>Gets the audit history.</summary>
    public FormHistory History { get; }

    /// <summary>Gets the linked workflow instance id, when opened from a workflow activity.</summary>
    public Guid? WorkflowInstanceId { get; private set; }

    /// <summary>Gets the linked workflow activity node id, when opened from a workflow activity.</summary>
    public string? WorkflowActivityNodeId { get; private set; }

    /// <summary>Gets a value indicating whether the instance has reached a terminal state.</summary>
    public bool IsFinished =>
        State is FormInstanceState.Approved or FormInstanceState.Rejected or FormInstanceState.Cancelled;

    /// <summary>Gets a value indicating whether the instance is bound to a workflow activity.</summary>
    public bool IsWorkflowBound => WorkflowInstanceId is not null && WorkflowActivityNodeId is not null;

    /// <summary>Creates a new form instance seeded with values.</summary>
    /// <param name="id">The instance id.</param>
    /// <param name="formKey">The form key.</param>
    /// <param name="version">The form version.</param>
    /// <param name="tenant">The owning tenant.</param>
    /// <param name="values">The seed values.</param>
    /// <returns>The new instance.</returns>
    public static FormInstance Create(
        Guid id, string formKey, FormVersion version, string tenant, FormValues? values = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        return new FormInstance(id, formKey, version, tenant, values ?? new FormValues());
    }

    /// <summary>Links the instance to a workflow activity it will complete on submission.</summary>
    /// <param name="workflowInstanceId">The workflow instance id.</param>
    /// <param name="activityNodeId">The workflow activity node id.</param>
    public void BindToWorkflow(Guid workflowInstanceId, string activityNodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activityNodeId);
        WorkflowInstanceId = workflowInstanceId;
        WorkflowActivityNodeId = activityNodeId;
    }

    /// <summary>Sets the resolved assignee.</summary>
    /// <param name="assignee">The assignee reference.</param>
    public void AssignTo(string? assignee) => Assignee = assignee;

    /// <summary>Moves the instance to the draft state.</summary>
    public void MarkDraft()
    {
        if (State == FormInstanceState.Open)
        {
            State = FormInstanceState.Draft;
        }
    }

    /// <summary>Moves the instance to the submitted state.</summary>
    /// <param name="submittedBy">Who submitted it.</param>
    public void MarkSubmitted(string? submittedBy)
    {
        SubmittedBy = submittedBy;
        State = FormInstanceState.Submitted;
    }

    /// <summary>Moves the instance to the approved state.</summary>
    public void MarkApproved() => State = FormInstanceState.Approved;

    /// <summary>Moves the instance to the rejected state.</summary>
    public void MarkRejected() => State = FormInstanceState.Rejected;

    /// <summary>Moves the instance to the cancelled state.</summary>
    public void MarkCancelled() => State = FormInstanceState.Cancelled;
}
