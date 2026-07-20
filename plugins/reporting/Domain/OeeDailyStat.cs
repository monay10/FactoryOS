namespace FactoryOS.Plugins.Reporting.Domain;

/// <summary>
/// A machine's aggregated OEE for one calendar day (UTC): how many readings folded in, their average, and the
/// best and worst seen. An immutable projection row a report or chart renders.
/// </summary>
/// <param name="Day">The UTC calendar day this row aggregates.</param>
/// <param name="SampleCount">How many OEE readings folded into the day.</param>
/// <param name="AverageOee">The mean OEE across the day's readings, a fraction in <c>[0, 1]</c>.</param>
/// <param name="MinOee">The lowest OEE reading of the day.</param>
/// <param name="MaxOee">The highest OEE reading of the day.</param>
public readonly record struct OeeDailyStat(
    DateOnly Day,
    int SampleCount,
    decimal AverageOee,
    decimal MinOee,
    decimal MaxOee);
