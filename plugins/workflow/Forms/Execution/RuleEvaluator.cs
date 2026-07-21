using FactoryOS.Plugins.Forms.Engine.Domain;

namespace FactoryOS.Plugins.Forms.Engine.Execution;

/// <summary>The resolved runtime shape of a form after its field rules have been evaluated.</summary>
/// <param name="Fields">The resolved presentation of each field, keyed by field key.</param>
/// <param name="Calculated">The values computed by calculated rules, keyed by field key.</param>
public sealed record FormEvaluation(
    IReadOnlyDictionary<string, FieldVisibility> Fields,
    IReadOnlyDictionary<string, object?> Calculated)
{
    /// <summary>Gets the resolved presentation of a field, or a default when the key is unknown.</summary>
    /// <param name="fieldKey">The field key.</param>
    /// <returns>The field visibility.</returns>
    public FieldVisibility For(string fieldKey) => Fields.TryGetValue(fieldKey, out var visibility)
        ? visibility
        : new FieldVisibility(fieldKey, Visible: true, Enabled: true, Required: false);
}

/// <summary>
/// Evaluates a form's conditional field rules against the current values, producing each field's resolved
/// visibility, enablement and required-ness, plus any values computed by calculated rules. Rules are applied
/// in declaration order; a later rule wins a conflicting toggle. Hidden fields are reported so validation can
/// skip them.
/// </summary>
public sealed class RuleEvaluator
{
    /// <summary>Evaluates every field's rules against a set of values.</summary>
    /// <param name="definition">The form definition.</param>
    /// <param name="values">The current form values.</param>
    /// <returns>The resolved evaluation.</returns>
    public FormEvaluation Evaluate(FormDefinition definition, IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(values);

        var fields = new Dictionary<string, FieldVisibility>(StringComparer.Ordinal);
        var calculated = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var field in definition.Fields.Values)
        {
            var visible = true;
            var enabled = true;
            var required = field.Validation.Required;

            foreach (var rule in field.Rules)
            {
                if (!rule.IsActive(values))
                {
                    continue;
                }

                switch (rule.Kind)
                {
                    case FieldRuleKind.Required:
                        required = true;
                        break;
                    case FieldRuleKind.ReadOnly:
                    case FieldRuleKind.Disabled:
                        enabled = false;
                        break;
                    case FieldRuleKind.Enabled:
                        enabled = true;
                        break;
                    case FieldRuleKind.Visible:
                        visible = true;
                        break;
                    case FieldRuleKind.Hidden:
                        visible = false;
                        break;
                    case FieldRuleKind.Calculated:
                        calculated[field.Key] = rule.Calculate(values);
                        break;
                    default:
                        break;
                }
            }

            fields[field.Key] = new FieldVisibility(field.Key, visible, enabled, required);
        }

        return new FormEvaluation(fields, calculated);
    }
}
