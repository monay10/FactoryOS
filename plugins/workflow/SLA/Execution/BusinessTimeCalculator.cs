using FactoryOS.Plugins.Workflow.SLA.Domain;

namespace FactoryOS.Plugins.Workflow.SLA.Execution;

/// <summary>
/// Converts between <b>business time</b> (the working hours an SLA budget is expressed in) and wall-clock
/// instants. It walks a calendar's working windows day by day, skipping closed hours, weekends and holidays, so
/// "four working hours from 16:00 on a Friday" lands on Monday morning rather than in the middle of the weekend.
/// A continuous 24x7 calendar short-circuits to plain arithmetic. The calculator is pure and side-effect free.
/// </summary>
public sealed class BusinessTimeCalculator
{
    // A calendar that cannot satisfy a budget within roughly ten years is misconfigured, not merely slow.
    private const int MaxDaysScanned = 3660;

    /// <summary>Finds the instant that lies a given amount of business time after a start.</summary>
    /// <param name="calendar">The calendar business time is counted on.</param>
    /// <param name="startUtc">The instant to count from.</param>
    /// <param name="businessDuration">The business-time budget to consume.</param>
    /// <returns>The instant the budget runs out.</returns>
    public DateTimeOffset Add(SlaCalendar calendar, DateTimeOffset startUtc, TimeSpan businessDuration)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        if (businessDuration <= TimeSpan.Zero)
        {
            return startUtc;
        }

        if (calendar.IsContinuous)
        {
            return startUtc + businessDuration;
        }

        var business = RequireWorkingCalendar(calendar);
        var offset = calendar.Zone.UtcOffset;
        var local = startUtc.ToOffset(offset).DateTime;
        var remaining = businessDuration;

        for (var scanned = 0; scanned < MaxDaysScanned; scanned++)
        {
            var day = DateOnly.FromDateTime(local);
            foreach (var window in business.WindowsFor(day))
            {
                var windowStart = day.ToDateTime(window.Start);
                var windowEnd = day.ToDateTime(window.End);
                if (local >= windowEnd)
                {
                    continue;
                }

                if (local < windowStart)
                {
                    local = windowStart;
                }

                var available = windowEnd - local;
                if (available >= remaining)
                {
                    return new DateTimeOffset(local + remaining, offset).ToUniversalTime();
                }

                remaining -= available;
                local = windowEnd;
            }

            local = day.AddDays(1).ToDateTime(TimeOnly.MinValue);
        }

        throw new InvalidOperationException(
            $"Calendar '{business.Key}' cannot satisfy a business-time budget of {businessDuration} within {MaxDaysScanned} days.");
    }

    /// <summary>Measures how much business time lies between two instants.</summary>
    /// <param name="calendar">The calendar business time is counted on.</param>
    /// <param name="fromUtc">The start instant.</param>
    /// <param name="toUtc">The end instant.</param>
    /// <returns>The business time between them; zero when the range is empty or inverted.</returns>
    public TimeSpan Elapsed(SlaCalendar calendar, DateTimeOffset fromUtc, DateTimeOffset toUtc)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        if (toUtc <= fromUtc)
        {
            return TimeSpan.Zero;
        }

        if (calendar.IsContinuous)
        {
            return toUtc - fromUtc;
        }

        var business = RequireWorkingCalendar(calendar);
        var offset = calendar.Zone.UtcOffset;
        var localFrom = fromUtc.ToOffset(offset).DateTime;
        var localTo = toUtc.ToOffset(offset).DateTime;

        var total = TimeSpan.Zero;
        var day = DateOnly.FromDateTime(localFrom);
        var lastDay = DateOnly.FromDateTime(localTo);

        for (var scanned = 0; day <= lastDay && scanned < MaxDaysScanned; scanned++, day = day.AddDays(1))
        {
            foreach (var window in business.WindowsFor(day))
            {
                var windowStart = day.ToDateTime(window.Start);
                var windowEnd = day.ToDateTime(window.End);
                var overlapStart = localFrom > windowStart ? localFrom : windowStart;
                var overlapEnd = localTo < windowEnd ? localTo : windowEnd;
                if (overlapEnd > overlapStart)
                {
                    total += overlapEnd - overlapStart;
                }
            }
        }

        return total;
    }

    /// <summary>Gets a value indicating whether an instant falls inside the calendar's working time.</summary>
    /// <param name="calendar">The calendar.</param>
    /// <param name="instantUtc">The instant to test.</param>
    /// <returns><see langword="true"/> when the instant is working time.</returns>
    public bool IsWorkingTime(SlaCalendar calendar, DateTimeOffset instantUtc)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        if (calendar.IsContinuous)
        {
            return true;
        }

        var business = RequireWorkingCalendar(calendar);
        var local = instantUtc.ToOffset(calendar.Zone.UtcOffset).DateTime;
        var day = DateOnly.FromDateTime(local);
        return business.WindowsFor(day).Any(window =>
            local >= day.ToDateTime(window.Start) && local < day.ToDateTime(window.End));
    }

    private static BusinessCalendar RequireWorkingCalendar(SlaCalendar calendar)
    {
        if (calendar.Business is not { } business || !business.HasWorkingTime)
        {
            throw new InvalidOperationException(
                "A business-calendar SLA needs a calendar that declares at least one working window.");
        }

        return business;
    }
}
