using FactoryOS.Plugins.Workflow.SLA.Domain;
using FactoryOS.Plugins.Workflow.SLA.Persistence;

namespace FactoryOS.Plugins.Workflow.SLA.Execution;

/// <summary>
/// Resolves the calendar an SLA counts time on. A 24x7 policy resolves to the continuous calendar; a business
/// policy resolves its named calendar from the calendar repository, so working hours and holidays are
/// configuration rather than code. A policy naming a calendar that is not registered is a configuration error
/// and is reported as one rather than silently falling back to a 24x7 clock.
/// </summary>
public sealed class CalendarEngine
{
    private readonly ISlaCalendarRepository _calendars;

    /// <summary>Initializes a new instance of the <see cref="CalendarEngine"/> class.</summary>
    /// <param name="calendars">The calendar repository.</param>
    public CalendarEngine(ISlaCalendarRepository calendars)
    {
        ArgumentNullException.ThrowIfNull(calendars);
        _calendars = calendars;
    }

    /// <summary>Resolves the calendar a policy runs on.</summary>
    /// <param name="policy">The SLA policy.</param>
    /// <returns>The resolved calendar.</returns>
    public SlaCalendar Resolve(SlaPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        return Resolve(policy.Kind, policy.CalendarKey);
    }

    /// <summary>Resolves the calendar a running SLA counts time on.</summary>
    /// <param name="sla">The SLA instance.</param>
    /// <returns>The resolved calendar.</returns>
    public SlaCalendar Resolve(SlaInstance sla)
    {
        ArgumentNullException.ThrowIfNull(sla);
        return Resolve(sla.CalendarKind, sla.CalendarKey);
    }

    /// <summary>Registers a business calendar.</summary>
    /// <param name="calendar">The calendar.</param>
    public void Register(BusinessCalendar calendar) => _calendars.Register(calendar);

    private SlaCalendar Resolve(SlaCalendarKind kind, string? calendarKey)
    {
        if (kind == SlaCalendarKind.Continuous)
        {
            return SlaCalendar.Continuous;
        }

        if (string.IsNullOrWhiteSpace(calendarKey))
        {
            throw new InvalidOperationException("A business-calendar SLA needs a calendar key.");
        }

        var calendar = _calendars.Get(calendarKey)
            ?? throw new InvalidOperationException($"Business calendar '{calendarKey}' is not registered.");

        return SlaCalendar.Of(calendar);
    }
}
