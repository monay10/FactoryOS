namespace FactoryOS.Shared.ValueObjects;

/// <summary>
/// A half-open range of instants <c>[Start, End)</c>. Immutable with value equality. The start must not be after the
/// end. Membership and overlap treat the end as exclusive, so adjacent ranges do not overlap.
/// </summary>
public sealed record DateRange
{
    private DateRange(DateTimeOffset start, DateTimeOffset end)
    {
        Start = start;
        End = end;
    }

    /// <summary>Gets the inclusive start instant.</summary>
    public DateTimeOffset Start { get; }

    /// <summary>Gets the exclusive end instant.</summary>
    public DateTimeOffset End { get; }

    /// <summary>Gets the duration of the range.</summary>
    public TimeSpan Duration => End - Start;

    /// <summary>Creates a range between two instants.</summary>
    /// <param name="start">The inclusive start.</param>
    /// <param name="end">The exclusive end; must not be before <paramref name="start"/>.</param>
    /// <returns>A new <see cref="DateRange"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="end"/> is before <paramref name="start"/>.</exception>
    public static DateRange Between(DateTimeOffset start, DateTimeOffset end)
    {
        if (end < start)
        {
            throw new ArgumentException("The end of a range must not be before its start.", nameof(end));
        }

        return new DateRange(start, end);
    }

    /// <summary>Determines whether an instant falls within the range (start inclusive, end exclusive).</summary>
    /// <param name="instant">The instant to test.</param>
    /// <returns><see langword="true"/> when the instant is in the range.</returns>
    public bool Contains(DateTimeOffset instant) => instant >= Start && instant < End;

    /// <summary>Determines whether this range overlaps another (exclusive at each end).</summary>
    /// <param name="other">The other range.</param>
    /// <returns><see langword="true"/> when the ranges overlap.</returns>
    public bool Overlaps(DateRange other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Start < other.End && other.Start < End;
    }
}
