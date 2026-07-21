using FactoryOS.Plugins.Workflow.SLA.Domain;

namespace FactoryOS.Plugins.Workflow.SLA.Execution;

/// <summary>
/// Decides when an SLA's deadline has been missed. A deadline breach is <b>not</b> the end of the SLA: the work
/// is still open, escalations still fire, and the SLA closes only when the work finishes or the hard timeout
/// hits. This is what keeps "expired" and "timed out" separate concepts.
/// </summary>
public sealed class DeadlineEngine
{
    /// <summary>Gets a value indicating whether the SLA's deadline has just been missed.</summary>
    /// <param name="sla">The SLA.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <returns><see langword="true"/> when the SLA is active and past its deadline.</returns>
    public bool IsBreached(SlaInstance sla, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(sla);
        return sla.Status == SlaStatus.Active && nowUtc >= sla.DueOnUtc;
    }

    /// <summary>Gets the business-time overrun of a breached SLA, on the wall clock.</summary>
    /// <param name="sla">The SLA.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <returns>How far past the deadline the SLA is; zero when it is not past it.</returns>
    public TimeSpan Overrun(SlaInstance sla, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(sla);
        return nowUtc > sla.DueOnUtc ? nowUtc - sla.DueOnUtc : TimeSpan.Zero;
    }
}

/// <summary>Decides which of an SLA's reminders are due. A reminder fires once and only while the SLA is open.</summary>
public sealed class ReminderEngine
{
    /// <summary>Gets the reminders that are due and have not fired.</summary>
    /// <param name="sla">The SLA.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <returns>The due reminders.</returns>
    public IReadOnlyList<SlaReminderState> Due(SlaInstance sla, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(sla);
        if (!sla.IsRunning)
        {
            return [];
        }

        return sla.Reminders.Where(reminder => !reminder.Fired && reminder.DueOnUtc <= nowUtc).ToArray();
    }
}

/// <summary>
/// Decides which of an SLA's escalation rungs are due. Escalations fire after the deadline, each once, and only
/// while the SLA is open — a completed or timed-out SLA escalates no further.
/// </summary>
public sealed class EscalationEngine
{
    /// <summary>Gets the escalation rungs that are due and have not fired.</summary>
    /// <param name="sla">The SLA.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <returns>The due escalations, lowest level first.</returns>
    public IReadOnlyList<SlaEscalationState> Due(SlaInstance sla, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(sla);
        if (!sla.IsRunning)
        {
            return [];
        }

        return sla.Escalations
            .Where(escalation => !escalation.Fired && escalation.DueOnUtc <= nowUtc)
            .OrderBy(escalation => escalation.Escalation.Level)
            .ToArray();
    }
}

/// <summary>
/// Decides when an SLA's hard timeout has hit. Unlike a deadline breach this is terminal: the SLA stops waiting
/// for the work and finishes as <see cref="SlaOutcome.TimedOut"/>, which is the disposition an SLA report reads
/// as "we gave up", distinct from "we finished late".
/// </summary>
public sealed class TimeoutEngine
{
    /// <summary>Gets a value indicating whether the SLA's hard timeout has passed.</summary>
    /// <param name="sla">The SLA.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <returns><see langword="true"/> when the SLA is running and past its timeout.</returns>
    public bool IsTimedOut(SlaInstance sla, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(sla);
        return sla.IsRunning && sla.TimeoutOnUtc is { } timeout && nowUtc >= timeout;
    }
}
