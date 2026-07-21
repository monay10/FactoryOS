using FactoryOS.Plugins.Workflow.Engine.Expressions;

namespace FactoryOS.Plugins.Forms.Engine.Domain;

/// <summary>A selectable option offered by a choice field (dropdown, radio, multi-select, …).</summary>
/// <param name="Value">The stored value.</param>
/// <param name="Label">The displayed label.</param>
public sealed record FieldOption(string Value, string Label);

/// <summary>
/// The declarative constraints a field's value must satisfy. Every property is optional; an unset constraint
/// is not checked. These are static bounds — behaviour that depends on other fields is expressed with a
/// <see cref="FieldRule"/> instead.
/// </summary>
public sealed record FieldValidation
{
    /// <summary>Gets a value indicating whether the field must always have a value.</summary>
    public bool Required { get; init; }

    /// <summary>Gets an optional regular expression the value's text form must match.</summary>
    public string? Pattern { get; init; }

    /// <summary>Gets an optional inclusive lower bound for numeric values.</summary>
    public decimal? Min { get; init; }

    /// <summary>Gets an optional inclusive upper bound for numeric values.</summary>
    public decimal? Max { get; init; }

    /// <summary>Gets an optional minimum length for text values.</summary>
    public int? MinLength { get; init; }

    /// <summary>Gets an optional maximum length for text values.</summary>
    public int? MaxLength { get; init; }
}

/// <summary>
/// A boolean condition over the form's current values, expressed in the shared workflow expression language
/// (e.g. <c>amount &gt; 1000</c>). Used to drive conditional visibility, validation and calculation.
/// </summary>
public sealed class FieldCondition
{
    private readonly WorkflowExpression _expression;

    /// <summary>Initializes a new instance of the <see cref="FieldCondition"/> class.</summary>
    /// <param name="expression">The boolean expression text.</param>
    public FieldCondition(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        Text = expression;
        _expression = WorkflowExpression.Parse(expression);
    }

    /// <summary>Gets the original expression text.</summary>
    public string Text { get; }

    /// <summary>Evaluates the condition against a set of values.</summary>
    /// <param name="values">The form values keyed by field key.</param>
    /// <returns><see langword="true"/> when the condition holds.</returns>
    public bool IsSatisfiedBy(IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return _expression.EvaluateBoolean(values);
    }
}

/// <summary>
/// A conditional behaviour applied to a field: when <see cref="When"/> holds (or always, when it is
/// <see langword="null"/>), the field takes on the effect named by <see cref="Kind"/>. For
/// <see cref="FieldRuleKind.Calculated"/> the <see cref="Expression"/> supplies the computed value.
/// </summary>
public sealed class FieldRule
{
    private readonly WorkflowExpression? _valueExpression;

    /// <summary>Initializes a new instance of the <see cref="FieldRule"/> class.</summary>
    /// <param name="kind">The effect the rule applies.</param>
    /// <param name="when">The condition that activates the rule, or <see langword="null"/> to always apply.</param>
    /// <param name="expression">The value expression, required for <see cref="FieldRuleKind.Calculated"/>.</param>
    public FieldRule(FieldRuleKind kind, FieldCondition? when = null, string? expression = null)
    {
        if (kind == FieldRuleKind.Calculated)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(expression);
            _valueExpression = WorkflowExpression.Parse(expression);
        }

        Kind = kind;
        When = when;
        Expression = expression;
    }

    /// <summary>Gets the effect the rule applies.</summary>
    public FieldRuleKind Kind { get; }

    /// <summary>Gets the condition that activates the rule, or <see langword="null"/> when it always applies.</summary>
    public FieldCondition? When { get; }

    /// <summary>Gets the value expression text for a calculated rule.</summary>
    public string? Expression { get; }

    /// <summary>Determines whether the rule is active for the given values.</summary>
    /// <param name="values">The form values.</param>
    /// <returns><see langword="true"/> when the rule applies.</returns>
    public bool IsActive(IReadOnlyDictionary<string, object?> values) => When is null || When.IsSatisfiedBy(values);

    /// <summary>Computes the value of a calculated rule.</summary>
    /// <param name="values">The form values.</param>
    /// <returns>The computed value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the rule is not a calculation.</exception>
    public object? Calculate(IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (_valueExpression is null)
        {
            throw new InvalidOperationException("Only a calculated rule can compute a value.");
        }

        return _valueExpression.Evaluate(values);
    }
}

/// <summary>The resolved runtime presentation of a field after its rules have been evaluated.</summary>
/// <param name="FieldKey">The field the state belongs to.</param>
/// <param name="Visible">Whether the field is shown.</param>
/// <param name="Enabled">Whether the field accepts input.</param>
/// <param name="Required">Whether a value is mandatory.</param>
public sealed record FieldVisibility(string FieldKey, bool Visible, bool Enabled, bool Required);

/// <summary>
/// The design-time description of a single form field: its stable key, label, type, static validation, the
/// options it offers (for choice fields) and the conditional rules that shape it at runtime.
/// </summary>
public sealed record FieldDefinition
{
    /// <summary>Initializes a new instance of the <see cref="FieldDefinition"/> record.</summary>
    /// <param name="key">The field key, unique within the form.</param>
    /// <param name="label">The display label.</param>
    /// <param name="type">The field type.</param>
    public FieldDefinition(string key, string label, FieldType type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        Key = key;
        Label = label;
        Type = type;
    }

    /// <summary>Gets the field key, unique within the form.</summary>
    public string Key { get; }

    /// <summary>Gets the display label.</summary>
    public string Label { get; }

    /// <summary>Gets the field type.</summary>
    public FieldType Type { get; }

    /// <summary>Gets the static validation constraints.</summary>
    public FieldValidation Validation { get; init; } = new();

    /// <summary>Gets the default value applied when the instance opens.</summary>
    public object? DefaultValue { get; init; }

    /// <summary>Gets the options offered by a choice field.</summary>
    public IReadOnlyList<FieldOption> Options { get; init; } = [];

    /// <summary>Gets the conditional rules that shape the field at runtime.</summary>
    public IReadOnlyList<FieldRule> Rules { get; init; } = [];

    /// <summary>Gets a value indicating whether the field carries a value (as opposed to being decorative).</summary>
    public bool IsInput => Type is not (FieldType.Label or FieldType.Separator);
}

/// <summary>The placement of a field within a group, spanning one or more grid columns.</summary>
/// <param name="Field">The field placed here.</param>
/// <param name="ColumnSpan">How many grid columns the field spans (1 or greater).</param>
public sealed record FormField(FieldDefinition Field, int ColumnSpan = 1)
{
    /// <summary>Gets the number of grid columns the field spans.</summary>
    public int ColumnSpan { get; } = ColumnSpan >= 1
        ? ColumnSpan
        : throw new ArgumentOutOfRangeException(nameof(ColumnSpan), ColumnSpan, "Column span must be 1 or greater.");
}
