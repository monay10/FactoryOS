namespace FactoryOS.Plugins.Workflow.SLA.Domain;

/// <summary>
/// How an SLA counts time and whether its clock may be stopped: a 24x7 clock or a named business calendar
/// (working hours plus a holiday calendar), and whether pausing is allowed. The policy is data — the runtime
/// resolves the named calendar at start and never branches on a customer.
/// </summary>
public sealed record SlaPolicy
{
    /// <summary>Initializes a new instance of the <see cref="SlaPolicy"/> record.</summary>
    /// <param name="kind">How time is counted.</param>
    /// <param name="calendarKey">The business calendar key, when the policy uses one.</param>
    /// <param name="allowPause">Whether the clock may be stopped and restarted.</param>
    public SlaPolicy(SlaCalendarKind kind, string? calendarKey = null, bool allowPause = true)
    {
        if (kind == SlaCalendarKind.BusinessCalendar && string.IsNullOrWhiteSpace(calendarKey))
        {
            throw new ArgumentException("A business-calendar policy needs a calendar key.", nameof(calendarKey));
        }

        Kind = kind;
        CalendarKey = calendarKey;
        AllowPause = allowPause;
    }

    /// <summary>Gets how time is counted.</summary>
    public SlaCalendarKind Kind { get; }

    /// <summary>Gets the business calendar key, when the policy uses one.</summary>
    public string? CalendarKey { get; }

    /// <summary>Gets a value indicating whether the clock may be stopped and restarted.</summary>
    public bool AllowPause { get; }

    /// <summary>A 24x7 policy on which every hour counts.</summary>
    public static SlaPolicy TwentyFourSeven { get; } = new(SlaCalendarKind.Continuous);

    /// <summary>Creates a policy that counts only the working time of a named business calendar.</summary>
    /// <param name="calendarKey">The business calendar key.</param>
    /// <param name="allowPause">Whether the clock may be stopped and restarted.</param>
    /// <returns>The policy.</returns>
    public static SlaPolicy WorkingHours(string calendarKey, bool allowPause = true) =>
        new(SlaCalendarKind.BusinessCalendar, calendarKey, allowPause);
}
