using FactoryOS.Plugins.Workflow.SLA.Domain;

namespace FactoryOS.Plugins.Workflow.SLA.Execution;

/// <summary>Everything that has become due on one SLA at a point in time.</summary>
/// <param name="Reminders">The reminders that should fire.</param>
/// <param name="Breached">Whether the deadline has just been missed.</param>
/// <param name="Escalations">The escalation rungs that should fire.</param>
/// <param name="TimedOut">Whether the hard timeout has passed.</param>
public sealed record SlaDueWork(
    IReadOnlyList<SlaReminderState> Reminders,
    bool Breached,
    IReadOnlyList<SlaEscalationState> Escalations,
    bool TimedOut)
{
    /// <summary>Gets a value indicating whether anything at all is due.</summary>
    public bool Any => Reminders.Count > 0 || Breached || Escalations.Count > 0 || TimedOut;
}

/// <summary>
/// Composes the deadline, reminder, escalation and timeout engines into a single read of what an SLA owes at a
/// point in time. It is pure: it decides what is due and changes nothing — applying the result is the runtime's
/// job. Keeping the decision separate from the mutation is what makes the timing rules unit-testable without a
/// clock, a store or an event bus.
/// </summary>
public sealed class SlaEvaluator
{
    private readonly DeadlineEngine _deadlines;
    private readonly ReminderEngine _reminders;
    private readonly EscalationEngine _escalations;
    private readonly TimeoutEngine _timeouts;

    /// <summary>Initializes a new instance of the <see cref="SlaEvaluator"/> class.</summary>
    /// <param name="deadlines">The deadline engine.</param>
    /// <param name="reminders">The reminder engine.</param>
    /// <param name="escalations">The escalation engine.</param>
    /// <param name="timeouts">The timeout engine.</param>
    public SlaEvaluator(
        DeadlineEngine deadlines,
        ReminderEngine reminders,
        EscalationEngine escalations,
        TimeoutEngine timeouts)
    {
        ArgumentNullException.ThrowIfNull(deadlines);
        ArgumentNullException.ThrowIfNull(reminders);
        ArgumentNullException.ThrowIfNull(escalations);
        ArgumentNullException.ThrowIfNull(timeouts);
        _deadlines = deadlines;
        _reminders = reminders;
        _escalations = escalations;
        _timeouts = timeouts;
    }

    /// <summary>Reads everything that has become due on an SLA.</summary>
    /// <param name="sla">The SLA.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <returns>The due work.</returns>
    public SlaDueWork Evaluate(SlaInstance sla, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(sla);

        // A paused or finished SLA owes nothing: its clock is not running.
        if (!sla.IsRunning)
        {
            return new SlaDueWork([], false, [], false);
        }

        return new SlaDueWork(
            _reminders.Due(sla, nowUtc),
            _deadlines.IsBreached(sla, nowUtc),
            _escalations.Due(sla, nowUtc),
            _timeouts.IsTimedOut(sla, nowUtc));
    }
}
