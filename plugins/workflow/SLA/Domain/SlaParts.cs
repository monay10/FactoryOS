namespace FactoryOS.Plugins.Workflow.SLA.Domain;

/// <summary>
/// How long the tracked work has, measured in <b>business time</b> on the SLA's calendar. Eight hours on a
/// weekday calendar means eight working hours, not eight wall-clock hours.
/// </summary>
/// <param name="Duration">The business-time budget.</param>
public sealed record SlaDeadline(TimeSpan Duration)
{
    /// <summary>Creates a deadline that allows the given business-time budget.</summary>
    /// <param name="duration">The budget.</param>
    /// <returns>The deadline.</returns>
    public static SlaDeadline In(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "A deadline must be a positive duration.");
        }

        return new SlaDeadline(duration);
    }
}

/// <summary>A reminder that fires a given amount of business time <b>before</b> the deadline.</summary>
/// <param name="Before">How far ahead of the deadline the reminder fires.</param>
public sealed record SlaReminder(TimeSpan Before);

/// <summary>
/// An escalation that fires a given amount of business time <b>after</b> the deadline has passed, raising the
/// SLA to the next level and naming who it escalates to.
/// </summary>
/// <param name="After">How long after the deadline the escalation fires.</param>
/// <param name="Assignee">Who the work escalates to.</param>
/// <param name="Level">The escalation level this rung represents.</param>
public sealed record SlaEscalation(TimeSpan After, string Assignee, int Level = 1);

/// <summary>
/// The hard limit: business time after the deadline at which the SLA stops waiting altogether and times out.
/// Distinct from the deadline — breaching a deadline records a miss and keeps escalating, whereas a timeout is
/// terminal and tells the consumer to stop waiting for the work.
/// </summary>
/// <param name="After">How long after the deadline the SLA times out.</param>
public sealed record SlaTimeout(TimeSpan After);

/// <summary>Why an SLA clock was stopped.</summary>
/// <param name="Code">A stable reason code (e.g. <c>waiting-on-customer</c>).</param>
/// <param name="Detail">An optional human-readable detail.</param>
public sealed record PauseReason(string Code, string? Detail = null)
{
    /// <summary>The reason used when a caller supplies none.</summary>
    public static PauseReason Unspecified { get; } = new("unspecified");
}

/// <summary>Why an SLA clock was restarted.</summary>
/// <param name="Code">A stable reason code (e.g. <c>customer-responded</c>).</param>
/// <param name="Detail">An optional human-readable detail.</param>
public sealed record ResumeReason(string Code, string? Detail = null)
{
    /// <summary>The reason used when a caller supplies none.</summary>
    public static ResumeReason Unspecified { get; } = new("unspecified");
}

/// <summary>Grants a principal rights over the SLAs produced by a definition.</summary>
/// <param name="Principal">The principal (a user id, <c>role:x</c> or <c>group:x</c>).</param>
/// <param name="Permission">The rights granted.</param>
public sealed record SlaPermissionGrant(string Principal, SlaPermission Permission);

/// <summary>A reminder's runtime state: when it is due on the wall clock, and whether it has fired.</summary>
public sealed class SlaReminderState
{
    /// <summary>Initializes a new instance of the <see cref="SlaReminderState"/> class.</summary>
    /// <param name="before">How far ahead of the deadline it fires.</param>
    /// <param name="dueOnUtc">When it is due.</param>
    public SlaReminderState(TimeSpan before, DateTimeOffset dueOnUtc)
    {
        Before = before;
        DueOnUtc = dueOnUtc;
    }

    /// <summary>Gets how far ahead of the deadline the reminder fires.</summary>
    public TimeSpan Before { get; }

    /// <summary>Gets when the reminder is due.</summary>
    public DateTimeOffset DueOnUtc { get; private set; }

    /// <summary>Gets a value indicating whether the reminder has fired.</summary>
    public bool Fired { get; private set; }

    /// <summary>Marks the reminder as fired.</summary>
    public void MarkFired() => Fired = true;

    /// <summary>Moves the reminder's due time (used when a pause shifts the schedule).</summary>
    /// <param name="dueOnUtc">The new due time.</param>
    public void RescheduleTo(DateTimeOffset dueOnUtc) => DueOnUtc = dueOnUtc;
}

/// <summary>An escalation's runtime state: when it is due on the wall clock, and whether it has fired.</summary>
public sealed class SlaEscalationState
{
    /// <summary>Initializes a new instance of the <see cref="SlaEscalationState"/> class.</summary>
    /// <param name="escalation">The escalation rung.</param>
    /// <param name="dueOnUtc">When it is due.</param>
    public SlaEscalationState(SlaEscalation escalation, DateTimeOffset dueOnUtc)
    {
        ArgumentNullException.ThrowIfNull(escalation);
        Escalation = escalation;
        DueOnUtc = dueOnUtc;
    }

    /// <summary>Gets the escalation rung.</summary>
    public SlaEscalation Escalation { get; }

    /// <summary>Gets when the escalation is due.</summary>
    public DateTimeOffset DueOnUtc { get; private set; }

    /// <summary>Gets a value indicating whether the escalation has fired.</summary>
    public bool Fired { get; private set; }

    /// <summary>Marks the escalation as fired.</summary>
    public void MarkFired() => Fired = true;

    /// <summary>Moves the escalation's due time (used when a pause shifts the schedule).</summary>
    /// <param name="dueOnUtc">The new due time.</param>
    public void RescheduleTo(DateTimeOffset dueOnUtc) => DueOnUtc = dueOnUtc;
}
