using FactoryOS.Plugins.Workflow.SLA.Domain;

namespace FactoryOS.Plugins.Workflow.SLA.Execution;

/// <summary>The wall-clock schedule a definition's business-time budgets resolve to at a given start.</summary>
/// <param name="DueOnUtc">When the overall deadline falls.</param>
/// <param name="Reminders">The resolved reminder schedule.</param>
/// <param name="Escalations">The resolved escalation schedule.</param>
/// <param name="TimeoutOnUtc">When the hard timeout falls, if there is one.</param>
public sealed record SlaSchedule(
    DateTimeOffset DueOnUtc,
    IReadOnlyList<SlaReminderState> Reminders,
    IReadOnlyList<SlaEscalationState> Escalations,
    DateTimeOffset? TimeoutOnUtc);

/// <summary>
/// Turns a definition's business-time budgets into wall-clock instants on a calendar: the deadline, each
/// reminder (a budget before the deadline), each escalation and the hard timeout (budgets after it), and each
/// stage's due time. It also computes the forward shift applied when a paused clock restarts. All arithmetic
/// goes through the business-time calculator, so working hours and holidays are honoured everywhere.
/// </summary>
public sealed class SlaScheduler
{
    private readonly BusinessTimeCalculator _calculator;

    /// <summary>Initializes a new instance of the <see cref="SlaScheduler"/> class.</summary>
    /// <param name="calculator">The business-time calculator.</param>
    public SlaScheduler(BusinessTimeCalculator calculator)
    {
        ArgumentNullException.ThrowIfNull(calculator);
        _calculator = calculator;
    }

    /// <summary>Builds the wall-clock schedule for a definition starting now.</summary>
    /// <param name="definition">The SLA definition.</param>
    /// <param name="calendar">The calendar time is counted on.</param>
    /// <param name="startUtc">When the SLA starts.</param>
    /// <returns>The resolved schedule.</returns>
    public SlaSchedule Build(SlaDefinition definition, SlaCalendar calendar, DateTimeOffset startUtc)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(calendar);

        var dueOn = _calculator.Add(calendar, startUtc, definition.Deadline.Duration);

        var reminders = definition.Reminders
            .Select(reminder =>
            {
                // A reminder set further out than the whole budget is due immediately rather than in the past.
                var lead = definition.Deadline.Duration - reminder.Before;
                var dueAt = lead <= TimeSpan.Zero ? startUtc : _calculator.Add(calendar, startUtc, lead);
                return new SlaReminderState(reminder.Before, dueAt);
            })
            .ToArray();

        var escalations = definition.Escalations
            .Select(escalation => new SlaEscalationState(escalation, _calculator.Add(calendar, dueOn, escalation.After)))
            .ToArray();

        var timeoutOn = definition.Timeout is { } timeout
            ? _calculator.Add(calendar, dueOn, timeout.After)
            : (DateTimeOffset?)null;

        return new SlaSchedule(dueOn, reminders, escalations, timeoutOn);
    }

    /// <summary>Computes when a stage that starts now is due.</summary>
    /// <param name="calendar">The calendar time is counted on.</param>
    /// <param name="startUtc">When the stage starts.</param>
    /// <param name="duration">The stage's business-time budget.</param>
    /// <returns>The stage's due time.</returns>
    public DateTimeOffset StageDue(SlaCalendar calendar, DateTimeOffset startUtc, TimeSpan duration) =>
        _calculator.Add(calendar, startUtc, duration);

    /// <summary>Measures the business time a pause consumed.</summary>
    /// <param name="calendar">The calendar time is counted on.</param>
    /// <param name="pausedOnUtc">When the clock stopped.</param>
    /// <param name="resumedOnUtc">When the clock restarted.</param>
    /// <returns>The business time that elapsed while paused.</returns>
    public TimeSpan PausedBusinessTime(
        SlaCalendar calendar, DateTimeOffset pausedOnUtc, DateTimeOffset resumedOnUtc) =>
        _calculator.Elapsed(calendar, pausedOnUtc, resumedOnUtc);

    /// <summary>Builds the shift applied to every remaining scheduled time when a paused clock restarts.</summary>
    /// <param name="calendar">The calendar time is counted on.</param>
    /// <param name="pausedBusinessTime">The business time the pause consumed.</param>
    /// <returns>A function mapping a scheduled time to its post-pause time.</returns>
    public Func<DateTimeOffset, DateTimeOffset> ShiftBy(SlaCalendar calendar, TimeSpan pausedBusinessTime)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        return scheduled => _calculator.Add(calendar, scheduled, pausedBusinessTime);
    }
}
