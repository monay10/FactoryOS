using FactoryOS.Plugins.Forms.Engine.Domain;

namespace FactoryOS.Plugins.Forms.Engine.Rendering;

/// <summary>A field as resolved for display: its value, its runtime presentation and its choice options.</summary>
/// <param name="FieldKey">The field key.</param>
/// <param name="Label">The display label.</param>
/// <param name="Type">The field type.</param>
/// <param name="Value">The current value.</param>
/// <param name="Enabled">Whether the field accepts input.</param>
/// <param name="Required">Whether a value is mandatory.</param>
/// <param name="ColumnSpan">How many grid columns the field spans.</param>
/// <param name="Options">The options offered by a choice field.</param>
public sealed record RenderedField(
    string FieldKey,
    string Label,
    FieldType Type,
    object? Value,
    bool Enabled,
    bool Required,
    int ColumnSpan,
    IReadOnlyList<FieldOption> Options);

/// <summary>A group of visible fields, arranged into grid rows.</summary>
/// <param name="Key">The group key.</param>
/// <param name="Title">The group caption, if any.</param>
/// <param name="Fields">The visible fields, in order.</param>
/// <param name="Rows">The fields arranged into grid rows for the form's layout.</param>
public sealed record RenderedGroup(
    string Key,
    string? Title,
    IReadOnlyList<RenderedField> Fields,
    IReadOnlyList<IReadOnlyList<RenderedField>> Rows);

/// <summary>A section of visible groups.</summary>
/// <param name="Key">The section key.</param>
/// <param name="Title">The section title.</param>
/// <param name="Groups">The groups that have visible fields.</param>
public sealed record RenderedSection(string Key, string Title, IReadOnlyList<RenderedGroup> Groups);

/// <summary>The resolved layout of a rendered form.</summary>
/// <param name="Kind">The layout kind.</param>
/// <param name="Columns">The number of grid columns.</param>
public sealed record RenderedLayout(FormLayoutKind Kind, int Columns);

/// <summary>
/// A form resolved for a UI: its title, layout and section tree with only the currently visible fields, each
/// carrying its value and resolved enablement and required-ness. A pure read model — rendering never mutates
/// the instance.
/// </summary>
/// <param name="FormKey">The form key.</param>
/// <param name="InstanceId">The instance rendered.</param>
/// <param name="Title">The form title.</param>
/// <param name="State">The instance state.</param>
/// <param name="Layout">The resolved layout.</param>
/// <param name="Sections">The sections that have visible fields.</param>
public sealed record RenderedForm(
    string FormKey,
    Guid InstanceId,
    string Title,
    FormInstanceState State,
    RenderedLayout Layout,
    IReadOnlyList<RenderedSection> Sections);
