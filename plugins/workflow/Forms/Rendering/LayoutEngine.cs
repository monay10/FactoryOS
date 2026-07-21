using FactoryOS.Plugins.Forms.Engine.Domain;

namespace FactoryOS.Plugins.Forms.Engine.Rendering;

/// <summary>
/// Arranges a group's visible fields into rows for a form's layout. A stack layout puts every field on its
/// own row; a grid layout packs fields left to right until a row's column budget is spent, then wraps. A
/// field spanning more columns than remain starts a new row.
/// </summary>
public sealed class LayoutEngine
{
    /// <summary>Arranges fields into rows for the given layout.</summary>
    /// <param name="fields">The visible fields, in order.</param>
    /// <param name="layout">The form layout.</param>
    /// <returns>The fields grouped into rows.</returns>
    public IReadOnlyList<IReadOnlyList<RenderedField>> Arrange(IReadOnlyList<RenderedField> fields, FormLayout layout)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(layout);

        if (layout.Kind == FormLayoutKind.Stack)
        {
            return fields.Select(field => (IReadOnlyList<RenderedField>)[field]).ToArray();
        }

        var rows = new List<IReadOnlyList<RenderedField>>();
        var current = new List<RenderedField>();
        var used = 0;

        foreach (var field in fields)
        {
            var span = Math.Min(field.ColumnSpan, layout.Columns);
            if (current.Count > 0 && used + span > layout.Columns)
            {
                rows.Add(current);
                current = [];
                used = 0;
            }

            current.Add(field);
            used += span;
        }

        if (current.Count > 0)
        {
            rows.Add(current);
        }

        return rows;
    }
}
