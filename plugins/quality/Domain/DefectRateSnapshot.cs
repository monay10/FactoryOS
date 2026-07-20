namespace FactoryOS.Plugins.Quality.Domain;

/// <summary>
/// The aggregate of a rolling window of inspections: how many units were inspected and how many were defective.
/// <see cref="DefectRate"/> is their ratio, guarding against an empty window.
/// </summary>
/// <param name="InspectedUnits">Total units inspected across the window.</param>
/// <param name="DefectiveUnits">Total defective units across the window.</param>
public readonly record struct DefectRateSnapshot(int InspectedUnits, int DefectiveUnits)
{
    /// <summary>The defect rate over the window, as a fraction in <c>[0, 1]</c>; zero when nothing was inspected.</summary>
    public decimal DefectRate => InspectedUnits > 0 ? (decimal)DefectiveUnits / InspectedUnits : 0m;
}
