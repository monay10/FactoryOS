namespace FactoryOS.Plugins.Workflow.SLA.Domain;

/// <summary>
/// The fixed UTC offset a business calendar's wall clock runs on. A fixed offset (rather than a named platform
/// zone) keeps SLA arithmetic deterministic and identical on every host and in every container.
/// </summary>
/// <param name="Id">The zone identifier, for display and configuration (e.g. <c>Europe/Istanbul</c>).</param>
/// <param name="UtcOffset">The offset from UTC.</param>
public sealed record TimeZoneDefinition(string Id, TimeSpan UtcOffset)
{
    /// <summary>UTC itself — a zero offset.</summary>
    public static TimeZoneDefinition Utc { get; } = new("UTC", TimeSpan.Zero);
}

/// <summary>
/// One working window on one weekday, in the calendar's local wall-clock time. A weekday may have several
/// windows (a morning and an afternoon shift around a lunch break); time outside every window does not count.
/// </summary>
/// <param name="Day">The weekday the window applies to.</param>
/// <param name="Start">When the window opens.</param>
/// <param name="End">When the window closes (must be after <paramref name="Start"/>).</param>
public sealed record WorkingHours(DayOfWeek Day, TimeOnly Start, TimeOnly End)
{
    /// <summary>Gets the length of the window.</summary>
    public TimeSpan Duration => End - Start;

    /// <summary>Creates a window, validating that it closes after it opens.</summary>
    /// <param name="day">The weekday.</param>
    /// <param name="start">When the window opens.</param>
    /// <param name="end">When the window closes.</param>
    /// <returns>The window.</returns>
    public static WorkingHours Of(DayOfWeek day, TimeOnly start, TimeOnly end)
    {
        if (end <= start)
        {
            throw new ArgumentOutOfRangeException(nameof(end), end, "A working window must close after it opens.");
        }

        return new WorkingHours(day, start, end);
    }
}

/// <summary>A named set of non-working dates — public holidays and shutdowns — that business time skips.</summary>
public sealed class HolidayCalendar
{
    private readonly HashSet<DateOnly> _dates;

    /// <summary>Initializes a new instance of the <see cref="HolidayCalendar"/> class.</summary>
    /// <param name="key">The calendar key.</param>
    /// <param name="dates">The non-working dates.</param>
    public HolidayCalendar(string key, IEnumerable<DateOnly>? dates = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key;
        _dates = dates is null ? [] : [.. dates];
    }

    /// <summary>Gets the calendar key.</summary>
    public string Key { get; }

    /// <summary>Gets the non-working dates.</summary>
    public IReadOnlyCollection<DateOnly> Dates => _dates;

    /// <summary>Adds a non-working date.</summary>
    /// <param name="date">The date.</param>
    /// <returns>The same calendar, to allow chaining.</returns>
    public HolidayCalendar Add(DateOnly date)
    {
        _dates.Add(date);
        return this;
    }

    /// <summary>Gets a value indicating whether a date is a holiday.</summary>
    /// <param name="date">The date.</param>
    /// <returns><see langword="true"/> when the date is non-working.</returns>
    public bool IsHoliday(DateOnly date) => _dates.Contains(date);
}

/// <summary>
/// A working-time calendar: the zone its wall clock runs on, the working windows of each weekday, and the
/// holidays it skips. Business time accrues only inside a working window on a non-holiday day.
/// </summary>
public sealed class BusinessCalendar
{
    private readonly List<WorkingHours> _workingHours;

    /// <summary>Initializes a new instance of the <see cref="BusinessCalendar"/> class.</summary>
    /// <param name="key">The calendar key.</param>
    /// <param name="zone">The zone the wall clock runs on.</param>
    /// <param name="workingHours">The working windows.</param>
    /// <param name="holidays">The holiday calendar, if any.</param>
    public BusinessCalendar(
        string key,
        TimeZoneDefinition? zone = null,
        IEnumerable<WorkingHours>? workingHours = null,
        HolidayCalendar? holidays = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key;
        Zone = zone ?? TimeZoneDefinition.Utc;
        _workingHours = workingHours is null ? [] : [.. workingHours];
        Holidays = holidays;
    }

    /// <summary>Gets the calendar key.</summary>
    public string Key { get; }

    /// <summary>Gets the zone the wall clock runs on.</summary>
    public TimeZoneDefinition Zone { get; }

    /// <summary>Gets the holiday calendar, if any.</summary>
    public HolidayCalendar? Holidays { get; }

    /// <summary>Gets the working windows.</summary>
    public IReadOnlyList<WorkingHours> WorkingHours => _workingHours;

    /// <summary>Gets a value indicating whether the calendar has any working time at all.</summary>
    public bool HasWorkingTime => _workingHours.Count > 0;

    /// <summary>Adds a working window.</summary>
    /// <param name="window">The window.</param>
    /// <returns>The same calendar, to allow chaining.</returns>
    public BusinessCalendar AddWorkingHours(WorkingHours window)
    {
        ArgumentNullException.ThrowIfNull(window);
        _workingHours.Add(window);
        return this;
    }

    /// <summary>Adds the same working window to every day from Monday to Friday.</summary>
    /// <param name="start">When the window opens.</param>
    /// <param name="end">When the window closes.</param>
    /// <returns>The same calendar, to allow chaining.</returns>
    public BusinessCalendar AddWeekdays(TimeOnly start, TimeOnly end)
    {
        foreach (var day in new[]
                 {
                     DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                     DayOfWeek.Thursday, DayOfWeek.Friday,
                 })
        {
            _workingHours.Add(Domain.WorkingHours.Of(day, start, end));
        }

        return this;
    }

    /// <summary>Gets the working windows of a date, ordered, or an empty list on a holiday or non-working day.</summary>
    /// <param name="date">The local date.</param>
    /// <returns>The ordered windows.</returns>
    public IReadOnlyList<WorkingHours> WindowsFor(DateOnly date)
    {
        if (Holidays?.IsHoliday(date) == true)
        {
            return [];
        }

        return _workingHours
            .Where(window => window.Day == date.DayOfWeek)
            .OrderBy(window => window.Start)
            .ToArray();
    }
}

/// <summary>
/// The calendar an SLA actually counts time on: either a continuous 24x7 clock, where every hour counts, or a
/// <see cref="BusinessCalendar"/>, where only working hours on non-holiday days do. This is the single seam the
/// business-time calculator works against, so the two modes never leak into the arithmetic as branches.
/// </summary>
public sealed class SlaCalendar
{
    private SlaCalendar(SlaCalendarKind kind, BusinessCalendar? business)
    {
        Kind = kind;
        Business = business;
    }

    /// <summary>Gets how time is counted.</summary>
    public SlaCalendarKind Kind { get; }

    /// <summary>Gets the business calendar, when <see cref="Kind"/> is a business calendar.</summary>
    public BusinessCalendar? Business { get; }

    /// <summary>Gets the zone the calendar's wall clock runs on.</summary>
    public TimeZoneDefinition Zone => Business?.Zone ?? TimeZoneDefinition.Utc;

    /// <summary>A 24x7 clock on which every hour counts.</summary>
    public static SlaCalendar Continuous { get; } = new(SlaCalendarKind.Continuous, null);

    /// <summary>Creates a calendar that counts only the working time of a business calendar.</summary>
    /// <param name="calendar">The business calendar.</param>
    /// <returns>The SLA calendar.</returns>
    public static SlaCalendar Of(BusinessCalendar calendar)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        return new SlaCalendar(SlaCalendarKind.BusinessCalendar, calendar);
    }

    /// <summary>Gets a value indicating whether every hour counts (no working-hours arithmetic is needed).</summary>
    public bool IsContinuous => Kind == SlaCalendarKind.Continuous;

    /// <summary>Gets a value indicating whether the calendar has any working time at all.</summary>
    public bool HasWorkingTime => Business is null || Business.HasWorkingTime;
}
