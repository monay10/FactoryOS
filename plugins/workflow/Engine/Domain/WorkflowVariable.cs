namespace FactoryOS.Plugins.Workflow.Engine.Domain;

/// <summary>A named workflow variable and its current value.</summary>
/// <param name="Name">The variable name.</param>
/// <param name="Value">The variable value (a number, string, boolean or <see langword="null"/>).</param>
public sealed record WorkflowVariable(string Name, object? Value);

/// <summary>
/// A mutable bag of workflow variables carried by an instance and read by expressions. Keys are
/// case-sensitive names; values are simple scalars.
/// </summary>
public sealed class WorkflowVariables
{
    private readonly Dictionary<string, object?> _values;

    /// <summary>Initializes a new, empty variable bag.</summary>
    public WorkflowVariables() => _values = new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>Initializes a variable bag seeded from an existing map.</summary>
    /// <param name="values">The initial values.</param>
    public WorkflowVariables(IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = new Dictionary<string, object?>(values, StringComparer.Ordinal);
    }

    /// <summary>Gets the variables as a read-only map for expression evaluation.</summary>
    public IReadOnlyDictionary<string, object?> AsReadOnly() => _values;

    /// <summary>Sets a variable value.</summary>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The value.</param>
    public void Set(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _values[name] = value;
    }

    /// <summary>Gets a variable value, or <see langword="null"/> when unset.</summary>
    /// <param name="name">The variable name.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    public object? Get(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _values.TryGetValue(name, out var value) ? value : null;
    }

    /// <summary>Determines whether a variable is set.</summary>
    /// <param name="name">The variable name.</param>
    /// <returns><see langword="true"/> when present.</returns>
    public bool Has(string name) => _values.ContainsKey(name);

    /// <summary>Enumerates the variables as records.</summary>
    /// <returns>The variables.</returns>
    public IReadOnlyCollection<WorkflowVariable> ToCollection() =>
        _values.Select(pair => new WorkflowVariable(pair.Key, pair.Value)).ToArray();
}
