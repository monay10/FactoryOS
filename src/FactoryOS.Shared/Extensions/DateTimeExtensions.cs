namespace FactoryOS.Shared.Extensions;

/// <summary>Convenience extensions for <see cref="DateTimeOffset"/>.</summary>
public static class DateTimeExtensions
{
    /// <summary>Returns the first instant of the value's calendar day, preserving its offset.</summary>
    /// <param name="value">The instant.</param>
    /// <returns>Midnight at the start of the same day.</returns>
    public static DateTimeOffset StartOfDay(this DateTimeOffset value) => new(value.Date, value.Offset);

    /// <summary>Returns the last representable instant of the value's calendar day, preserving its offset.</summary>
    /// <param name="value">The instant.</param>
    /// <returns>The final tick of the same day.</returns>
    public static DateTimeOffset EndOfDay(this DateTimeOffset value) =>
        new(value.Date.AddDays(1).AddTicks(-1), value.Offset);

    /// <summary>Determines whether the instant falls in the half-open range <c>[start, end)</c>.</summary>
    /// <param name="value">The instant to test.</param>
    /// <param name="start">The inclusive start.</param>
    /// <param name="end">The exclusive end.</param>
    /// <returns><see langword="true"/> when the instant is within the range.</returns>
    public static bool IsBetween(this DateTimeOffset value, DateTimeOffset start, DateTimeOffset end) =>
        value >= start && value < end;
}
