namespace FactoryOS.Shared.ValueObjects;

/// <summary>
/// An inclusive range of calendar dates <c>[Start, End]</c> — for reporting or billing periods, where whole days
/// matter rather than instants. Immutable with value equality. The start must not be after the end.
/// </summary>
public sealed record Period
{
    private Period(DateOnly start, DateOnly end)
    {
        Start = start;
        End = end;
    }

    /// <summary>Gets the inclusive first day of the period.</summary>
    public DateOnly Start { get; }

    /// <summary>Gets the inclusive last day of the period.</summary>
    public DateOnly End { get; }

    /// <summary>Gets the number of whole days in the period (inclusive of both ends).</summary>
    public int Days => End.DayNumber - Start.DayNumber + 1;

    /// <summary>Creates a period between two calendar dates.</summary>
    /// <param name="start">The inclusive first day.</param>
    /// <param name="end">The inclusive last day; must not be before <paramref name="start"/>.</param>
    /// <returns>A new <see cref="Period"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="end"/> is before <paramref name="start"/>.</exception>
    public static Period Between(DateOnly start, DateOnly end)
    {
        if (end < start)
        {
            throw new ArgumentException("The end of a period must not be before its start.", nameof(end));
        }

        return new Period(start, end);
    }

    /// <summary>Creates the period covering a whole calendar month.</summary>
    /// <param name="year">The calendar year.</param>
    /// <param name="month">The calendar month (1–12).</param>
    /// <returns>A period spanning the month.</returns>
    public static Period ForMonth(int year, int month)
    {
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        return new Period(start, end);
    }

    /// <summary>Determines whether a date falls within the period (both ends inclusive).</summary>
    /// <param name="date">The date to test.</param>
    /// <returns><see langword="true"/> when the date is in the period.</returns>
    public bool Contains(DateOnly date) => date >= Start && date <= End;
}
