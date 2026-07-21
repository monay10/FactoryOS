using FactoryOS.Plugins.Forms.Engine.Configuration;
using FactoryOS.Plugins.Forms.Engine.Domain;
using FactoryOS.Plugins.Forms.Engine.Execution;
using FactoryOS.Plugins.Forms.Engine.Localization;

namespace FactoryOS.Plugins.Forms.Engine.Rendering;

/// <summary>Renders a single field into its display model, resolving its label through localization.</summary>
public static class FieldRenderer
{
    /// <summary>Renders a field placement given its resolved visibility and current value.</summary>
    /// <param name="formKey">The owning form key (used to build localization keys).</param>
    /// <param name="placement">The field placement.</param>
    /// <param name="visibility">The resolved visibility.</param>
    /// <param name="value">The current value.</param>
    /// <param name="localizer">The localizer.</param>
    /// <param name="culture">The requested culture.</param>
    /// <returns>The rendered field.</returns>
    public static RenderedField Render(
        string formKey,
        FormField placement,
        FieldVisibility visibility,
        object? value,
        IFormLocalizer localizer,
        string culture)
    {
        ArgumentNullException.ThrowIfNull(placement);
        ArgumentNullException.ThrowIfNull(visibility);
        ArgumentNullException.ThrowIfNull(localizer);
        var field = placement.Field;
        var label = localizer.Localize(culture, $"{formKey}.{field.Key}.label", field.Label);
        return new RenderedField(
            field.Key, label, field.Type, value, visibility.Enabled, visibility.Required, placement.ColumnSpan,
            field.Options);
    }
}

/// <summary>Renders a group's visible fields and arranges them into rows.</summary>
public static class GroupRenderer
{
    /// <summary>Renders a group, dropping fields the evaluation hides.</summary>
    /// <param name="formKey">The owning form key.</param>
    /// <param name="group">The group.</param>
    /// <param name="evaluation">The rule evaluation.</param>
    /// <param name="values">The current values.</param>
    /// <param name="layout">The form layout.</param>
    /// <param name="layoutEngine">The layout engine.</param>
    /// <param name="localizer">The localizer.</param>
    /// <param name="culture">The requested culture.</param>
    /// <returns>The rendered group, or <see langword="null"/> when no field is visible.</returns>
    public static RenderedGroup? Render(
        string formKey,
        FormGroup group,
        FormEvaluation evaluation,
        IReadOnlyDictionary<string, object?> values,
        FormLayout layout,
        LayoutEngine layoutEngine,
        IFormLocalizer localizer,
        string culture)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(evaluation);
        ArgumentNullException.ThrowIfNull(layoutEngine);

        var fields = new List<RenderedField>();
        foreach (var placement in group.Fields)
        {
            var visibility = evaluation.For(placement.Field.Key);
            if (!visibility.Visible)
            {
                continue;
            }

            var value = values.TryGetValue(placement.Field.Key, out var raw) ? raw : placement.Field.DefaultValue;
            fields.Add(FieldRenderer.Render(formKey, placement, visibility, value, localizer, culture));
        }

        if (fields.Count == 0)
        {
            return null;
        }

        var rows = layoutEngine.Arrange(fields, layout);
        return new RenderedGroup(group.Key, group.Title, fields, rows);
    }
}

/// <summary>Renders a section's groups, dropping any that have no visible fields.</summary>
public static class SectionRenderer
{
    /// <summary>Renders a section.</summary>
    /// <param name="formKey">The owning form key.</param>
    /// <param name="section">The section.</param>
    /// <param name="evaluation">The rule evaluation.</param>
    /// <param name="values">The current values.</param>
    /// <param name="layout">The form layout.</param>
    /// <param name="layoutEngine">The layout engine.</param>
    /// <param name="localizer">The localizer.</param>
    /// <param name="culture">The requested culture.</param>
    /// <returns>The rendered section, or <see langword="null"/> when no group is visible.</returns>
    public static RenderedSection? Render(
        string formKey,
        FormSection section,
        FormEvaluation evaluation,
        IReadOnlyDictionary<string, object?> values,
        FormLayout layout,
        LayoutEngine layoutEngine,
        IFormLocalizer localizer,
        string culture)
    {
        ArgumentNullException.ThrowIfNull(section);
        var groups = new List<RenderedGroup>();
        foreach (var group in section.Groups)
        {
            var rendered = GroupRenderer.Render(formKey, group, evaluation, values, layout, layoutEngine, localizer, culture);
            if (rendered is not null)
            {
                groups.Add(rendered);
            }
        }

        return groups.Count == 0
            ? null
            : new RenderedSection(section.Key, localizer.Localize(culture, $"{formKey}.{section.Key}.title", section.Title), groups);
    }
}

/// <summary>
/// Renders a form instance into a <see cref="RenderedForm"/> read model for a UI: it evaluates the field
/// rules against the instance's values, drops hidden fields, resolves labels through localization, and lays
/// the visible fields out per the form's layout.
/// </summary>
public sealed class FormRenderer
{
    private readonly RuleEvaluator _ruleEvaluator;
    private readonly LayoutEngine _layoutEngine;
    private readonly IFormLocalizer _localizer;

    /// <summary>Initializes a new instance of the <see cref="FormRenderer"/> class.</summary>
    /// <param name="ruleEvaluator">The rule evaluator.</param>
    /// <param name="layoutEngine">The layout engine.</param>
    /// <param name="localizer">The localizer.</param>
    public FormRenderer(RuleEvaluator ruleEvaluator, LayoutEngine layoutEngine, IFormLocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(ruleEvaluator);
        ArgumentNullException.ThrowIfNull(layoutEngine);
        ArgumentNullException.ThrowIfNull(localizer);
        _ruleEvaluator = ruleEvaluator;
        _layoutEngine = layoutEngine;
        _localizer = localizer;
    }

    /// <summary>Renders a form instance against its definition.</summary>
    /// <param name="definition">The form definition.</param>
    /// <param name="instance">The instance to render.</param>
    /// <param name="culture">The requested culture, or <see langword="null"/> for the default.</param>
    /// <returns>The rendered form.</returns>
    public RenderedForm Render(FormDefinition definition, FormInstance instance, string? culture = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(instance);

        var resolvedCulture = string.IsNullOrWhiteSpace(culture) ? FormConstants.DefaultCulture : culture;
        var values = instance.Values.AsReadOnly();
        var evaluation = _ruleEvaluator.Evaluate(definition, values);

        var sections = new List<RenderedSection>();
        foreach (var section in definition.Sections)
        {
            var rendered = SectionRenderer.Render(
                definition.Key, section, evaluation, values, definition.Layout, _layoutEngine, _localizer, resolvedCulture);
            if (rendered is not null)
            {
                sections.Add(rendered);
            }
        }

        var title = _localizer.Localize(resolvedCulture, $"{definition.Key}.title", definition.Title);
        var layout = new RenderedLayout(definition.Layout.Kind, definition.Layout.Columns);
        return new RenderedForm(definition.Key, instance.Id, title, instance.State, layout, sections);
    }
}
