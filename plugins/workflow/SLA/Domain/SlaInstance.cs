namespace FactoryOS.Plugins.Workflow.SLA.Domain;

/// <summary>
/// A running service-level agreement over one piece of work. It holds the wall-clock times its business-time
/// budget resolved to (deadline, reminders, escalations, stage due dates and the optional timeout), its
/// lifecycle state, and its audit trail. Pausing stops the clock: on resume every remaining time shifts forward
/// by the business time that elapsed while paused, so a suspension never silently consumes the budget.
/// The instance enforces the legal transitions; the runtime decides when to apply them.
/// </summary>
public sealed class SlaInstance
{
    private readonly List<SlaReminderState> _reminders;
    private readonly List<SlaEscalationState> _escalations;
    private readonly List<SlaStageState> _stages = [];

    /// <summary>Initializes a new instance of the <see cref="SlaInstance"/> class.</summary>
    /// <param name="tenant">The owning tenant.</param>
    /// <param name="definitionKey">The producing definition key.</param>
    /// <param name="target">The work being tracked.</param>
    /// <param name="startedOnUtc">When the SLA started.</param>
    /// <param name="dueOnUtc">When the overall deadline falls.</param>
    /// <param name="calendarKind">How time is counted.</param>
    /// <param name="reminders">The resolved reminder schedule.</param>
    /// <param name="escalations">The resolved escalation schedule.</param>
    /// <param name="timeoutOnUtc">When the hard timeout falls, if there is one.</param>
    /// <param name="calendarKey">The business calendar key, when one is used.</param>
    public SlaInstance(
        string tenant,
        string definitionKey,
        SlaTarget target,
        DateTimeOffset startedOnUtc,
        DateTimeOffset dueOnUtc,
        SlaCalendarKind calendarKind,
        IEnumerable<SlaReminderState>? reminders = null,
        IEnumerable<SlaEscalationState>? escalations = null,
        DateTimeOffset? timeoutOnUtc = null,
        string? calendarKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionKey);
        ArgumentNullException.ThrowIfNull(target);

        Id = Guid.NewGuid();
        Tenant = tenant;
        DefinitionKey = definitionKey;
        Target = target;
        StartedOnUtc = startedOnUtc;
        DueOnUtc = dueOnUtc;
        CalendarKind = calendarKind;
        CalendarKey = calendarKey;
        TimeoutOnUtc = timeoutOnUtc;
        Status = SlaStatus.Active;
        Outcome = SlaOutcome.None;
        _reminders = reminders is null ? [] : [.. reminders];
        _escalations = escalations is null ? [] : [.. escalations];
    }

    /// <summary>Gets the SLA id.</summary>
    public Guid Id { get; }

    /// <summary>Gets the owning tenant.</summary>
    public string Tenant { get; }

    /// <summary>Gets the producing definition key.</summary>
    public string DefinitionKey { get; }

    /// <summary>Gets the work being tracked.</summary>
    public SlaTarget Target { get; }

    /// <summary>Gets when the SLA started.</summary>
    public DateTimeOffset StartedOnUtc { get; }

    /// <summary>Gets when the overall deadline falls.</summary>
    public DateTimeOffset DueOnUtc { get; private set; }

    /// <summary>Gets when the hard timeout falls, if there is one.</summary>
    public DateTimeOffset? TimeoutOnUtc { get; private set; }

    /// <summary>Gets how time is counted.</summary>
    public SlaCalendarKind CalendarKind { get; }

    /// <summary>Gets the business calendar key, when one is used.</summary>
    public string? CalendarKey { get; }

    /// <summary>Gets the current lifecycle state.</summary>
    public SlaStatus Status { get; private set; }

    /// <summary>Gets the terminal disposition, once the SLA has finished.</summary>
    public SlaOutcome Outcome { get; private set; }

    /// <summary>Gets when the deadline was breached, if it was.</summary>
    public DateTimeOffset? BreachedOnUtc { get; private set; }

    /// <summary>Gets when the SLA finished, if it has.</summary>
    public DateTimeOffset? FinishedOnUtc { get; private set; }

    /// <summary>Gets when the clock was stopped, while it is stopped.</summary>
    public DateTimeOffset? PausedOnUtc { get; private set; }

    /// <summary>Gets why the clock was last stopped.</summary>
    public PauseReason? PausedBecause { get; private set; }

    /// <summary>Gets why the clock was last restarted.</summary>
    public ResumeReason? ResumedBecause { get; private set; }

    /// <summary>Gets the highest escalation level reached.</summary>
    public int EscalationLevel { get; private set; }

    /// <summary>Gets the resolved reminder schedule.</summary>
    public IReadOnlyList<SlaReminderState> Reminders => _reminders;

    /// <summary>Gets the resolved escalation schedule.</summary>
    public IReadOnlyList<SlaEscalationState> Escalations => _escalations;

    /// <summary>Gets the stage schedule, when the SLA is staged.</summary>
    public IReadOnlyList<SlaStageState> Stages => _stages;

    /// <summary>Gets the stage currently in progress, when the SLA is staged.</summary>
    public SlaStageState? CurrentStage =>
        _stages.LastOrDefault(stage => stage.CompletedOnUtc is null);

    /// <summary>Gets a value indicating whether the clock is running (not paused and not finished).</summary>
    public bool IsRunning => Status is SlaStatus.Active or SlaStatus.Breached;

    /// <summary>Gets a value indicating whether the SLA has finished.</summary>
    public bool IsTerminal => Status is SlaStatus.Completed or SlaStatus.TimedOut or SlaStatus.Cancelled;

    /// <summary>Gets a value indicating whether the SLA is open (running or paused).</summary>
    public bool IsOpen => !IsTerminal;

    /// <summary>Adds the first (or next) stage's runtime state.</summary>
    /// <param name="stage">The stage state to add.</param>
    public void AddStage(SlaStageState stage)
    {
        ArgumentNullException.ThrowIfNull(stage);
        _stages.Add(stage);
    }

    /// <summary>Records that the deadline passed while the work was still open.</summary>
    /// <param name="nowUtc">The current time.</param>
    /// <returns><see langword="true"/> when the SLA moved from active to breached.</returns>
    public bool Breach(DateTimeOffset nowUtc)
    {
        if (Status != SlaStatus.Active)
        {
            return false;
        }

        Status = SlaStatus.Breached;
        BreachedOnUtc = nowUtc;
        return true;
    }

    /// <summary>Stops the clock.</summary>
    /// <param name="nowUtc">The current time.</param>
    /// <param name="reason">Why the clock is stopping.</param>
    /// <returns><see langword="true"/> when the SLA was running and is now paused.</returns>
    public bool Pause(DateTimeOffset nowUtc, PauseReason? reason = null)
    {
        if (!IsRunning)
        {
            return false;
        }

        Status = SlaStatus.Paused;
        PausedOnUtc = nowUtc;
        PausedBecause = reason ?? PauseReason.Unspecified;
        return true;
    }

    /// <summary>
    /// Restarts the clock, shifting every remaining scheduled time forward so the paused interval does not
    /// consume the budget.
    /// </summary>
    /// <param name="reason">Why the clock is restarting.</param>
    /// <param name="shift">Maps a scheduled time to its post-pause time.</param>
    /// <returns><see langword="true"/> when the SLA was paused and is now running again.</returns>
    public bool Resume(ResumeReason? reason, Func<DateTimeOffset, DateTimeOffset> shift)
    {
        ArgumentNullException.ThrowIfNull(shift);
        if (Status != SlaStatus.Paused)
        {
            return false;
        }

        DueOnUtc = shift(DueOnUtc);
        if (TimeoutOnUtc is { } timeout)
        {
            TimeoutOnUtc = shift(timeout);
        }

        foreach (var reminder in _reminders.Where(reminder => !reminder.Fired))
        {
            reminder.RescheduleTo(shift(reminder.DueOnUtc));
        }

        foreach (var escalation in _escalations.Where(escalation => !escalation.Fired))
        {
            escalation.RescheduleTo(shift(escalation.DueOnUtc));
        }

        foreach (var stage in _stages.Where(stage => stage.CompletedOnUtc is null))
        {
            stage.RescheduleTo(shift(stage.DueOnUtc));
        }

        // A breach already recorded stays recorded; the SLA returns to whichever state it was in.
        Status = BreachedOnUtc is null ? SlaStatus.Active : SlaStatus.Breached;
        PausedOnUtc = null;
        ResumedBecause = reason ?? ResumeReason.Unspecified;
        return true;
    }

    /// <summary>Records that an escalation rung fired.</summary>
    /// <param name="level">The level reached.</param>
    public void RecordEscalation(int level) => EscalationLevel = Math.Max(EscalationLevel, level);

    /// <summary>Completes the current stage and starts the next one.</summary>
    /// <param name="nowUtc">The current time.</param>
    /// <param name="next">The next stage's runtime state, or <see langword="null"/> when this was the last.</param>
    /// <returns><see langword="true"/> when a stage was completed.</returns>
    public bool AdvanceStage(DateTimeOffset nowUtc, SlaStageState? next)
    {
        var current = CurrentStage;
        if (current is null || !IsOpen)
        {
            return false;
        }

        current.Complete(nowUtc);
        if (next is not null)
        {
            _stages.Add(next);
        }

        return true;
    }

    /// <summary>Finishes the SLA because the tracked work completed.</summary>
    /// <param name="nowUtc">The current time.</param>
    /// <returns><see langword="true"/> when the SLA was open and is now completed.</returns>
    public bool Complete(DateTimeOffset nowUtc)
    {
        if (IsTerminal)
        {
            return false;
        }

        CurrentStage?.Complete(nowUtc);
        Status = SlaStatus.Completed;
        Outcome = BreachedOnUtc is not null || nowUtc > DueOnUtc ? SlaOutcome.Breached : SlaOutcome.Met;
        FinishedOnUtc = nowUtc;
        return true;
    }

    /// <summary>Finishes the SLA because its hard timeout passed.</summary>
    /// <param name="nowUtc">The current time.</param>
    /// <returns><see langword="true"/> when the SLA was open and is now timed out.</returns>
    public bool TimeOut(DateTimeOffset nowUtc)
    {
        if (IsTerminal)
        {
            return false;
        }

        Status = SlaStatus.TimedOut;
        Outcome = SlaOutcome.TimedOut;
        FinishedOnUtc = nowUtc;
        return true;
    }

    /// <summary>Cancels the SLA before the tracked work finished.</summary>
    /// <param name="nowUtc">The current time.</param>
    /// <returns><see langword="true"/> when the SLA was open and is now cancelled.</returns>
    public bool Cancel(DateTimeOffset nowUtc)
    {
        if (IsTerminal)
        {
            return false;
        }

        Status = SlaStatus.Cancelled;
        Outcome = SlaOutcome.Cancelled;
        FinishedOnUtc = nowUtc;
        return true;
    }
}
