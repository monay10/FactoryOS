namespace FactoryOS.Plugins.Forms.Engine.Domain;

/// <summary>
/// A group of fields rendered together under an optional caption. Groups are the smallest structural unit and
/// hold the actual field placements.
/// </summary>
/// <param name="Key">The group key, unique within the form.</param>
/// <param name="Title">The optional group caption.</param>
/// <param name="Fields">The fields placed in the group.</param>
public sealed record FormGroup(string Key, string? Title, IReadOnlyList<FormField> Fields);

/// <summary>A titled section of a form containing one or more <see cref="FormGroup"/> groups.</summary>
/// <param name="Key">The section key, unique within the form.</param>
/// <param name="Title">The section title.</param>
/// <param name="Groups">The groups in the section.</param>
public sealed record FormSection(string Key, string Title, IReadOnlyList<FormGroup> Groups);

/// <summary>The layout strategy for a form: how many columns the grid has and how contents flow.</summary>
/// <param name="Kind">The layout kind.</param>
/// <param name="Columns">The number of grid columns (1 or greater; ignored for a stack).</param>
public sealed record FormLayout(FormLayoutKind Kind = FormLayoutKind.Stack, int Columns = 1)
{
    /// <summary>A single-column stacked layout.</summary>
    public static FormLayout Stack { get; } = new(FormLayoutKind.Stack, 1);

    /// <summary>Gets the number of grid columns.</summary>
    public int Columns { get; } = Columns >= 1
        ? Columns
        : throw new ArgumentOutOfRangeException(nameof(Columns), Columns, "Columns must be 1 or greater.");

    /// <summary>Creates a grid layout with the given number of columns.</summary>
    /// <param name="columns">The column count.</param>
    /// <returns>A grid layout.</returns>
    public static FormLayout Grid(int columns) => new(FormLayoutKind.Grid, columns);
}
