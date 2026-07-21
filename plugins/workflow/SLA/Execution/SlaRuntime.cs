using FactoryOS.Domain.Abstractions;
using FactoryOS.Plugins.Workflow.SLA.Configuration;
using FactoryOS.Plugins.Workflow.SLA.Diagnostics;
using FactoryOS.Plugins.Workflow.SLA.Domain;
using FactoryOS.Plugins.Workflow.SLA.Events;
using FactoryOS.Plugins.Workflow.SLA.Persistence;

namespace FactoryOS.Plugins.Workflow.SLA.Execution;

/// <summary>A summary of what one SLA due-work pass did.</summary>
/// <param name="Evaluated">How many open SLAs were examined.</param>
/// <param name="Reminders">How many reminders fired.</param>
/// <param name="Breaches">How many deadlines were missed.</param>
/// <param name="Escalations">How many escalations fired.</param>
/// <param name="TimedOut">How many SLAs hit their hard timeout.</param>
public sealed record SlaDueWorkSummary(int Evaluated, int Reminders, int Breaches, int Escalations, int TimedOut);

/// <summary>
/// The heart of the SLA engine: it starts SLAs over a target, stops and restarts their clocks, advances staged
/// SLAs, closes them when the tracked work finishes, and runs the due-work pass that fires reminders, records
/// breaches, escalates and times out. It holds only a <see cref="SlaTarget"/> reference to the work — it never
/// calls into the workflow, human task, approval, forms or notification engines. Everything it decides is
/// published as an event; who reacts is not its concern.
/// </summary>
public sealed class SlaRuntime
{
    private readonly SlaScheduler _scheduler;
    private readonly CalendarEngine _calendars;
    private readonly SlaEvaluator _evaluator;
    private readonly ISlaRepository _definitions;
    private readonly ISlaStore _store;
    private readonly ISlaHistoryRepository _history;
    private readonly IEnumerable<ISlaEventSink> _events;
    private readonly SlaMetrics _metrics;
    private readonly SlaEngineOptions _options;
    private readonly IDateTimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="SlaRuntime"/> class.</summary>
    /// <param name="scheduler">The scheduler.</param>
    /// <param name="calendars">The calendar engine.</param>
    /// <param name="evaluator">The due-work evaluator.</param>
    /// <param name="definitions">The definition repository.</param>
    /// <param name="store">The SLA store.</param>
    /// <param name="history">The history repository.</param>
    /// <param name="events">The event sinks the runtime fans out to.</param>
    /// <param name="metrics">The metrics counters.</param>
    /// <param name="options">The engine options.</param>
    /// <param name="clock">The clock.</param>
    public SlaRuntime(
        SlaScheduler scheduler,
        CalendarEngine calendars,
        SlaEvaluator evaluator,
        ISlaRepository definitions,
        ISlaStore store,
        ISlaHistoryRepository history,
        IEnumerable<ISlaEventSink> events,
        SlaMetrics metrics,
        SlaEngineOptions options,
        IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(calendars);
        ArgumentNullException.ThrowIfNull(evaluator);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        _scheduler = scheduler;
        _calendars = calendars;
        _evaluator = evaluator;
        _definitions = definitions;
        _store = store;
        _history = history;
        _events = events;
        _metrics = metrics;
        _options = options;
        _clock = clock;
    }

    /// <summary>Registers an SLA definition.</summary>
    /// <param name="definition">The definition.</param>
    public void Register(SlaDefinition definition) => _definitions.Register(definition);

    /// <summary>Starts an SLA tracking a target.</summary>
    /// <param name="definition">The SLA definition.</param>
    /// <param name="target">The work to track.</param>
    /// <param name="context">The context (tenant, initiator, culture).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The started SLA.</returns>
    public Task<SlaInstance> StartAsync(
        SlaDefinition definition,
        SlaTarget target,
        SlaContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (_options.AutoRegisterDefinitions)
        {
            _definitions.Register(definition);
        }

        var now = _clock.UtcNow;
        var calendar = _calendars.Resolve(definition.Policy);
        var schedule = _scheduler.Build(definition, calendar, now);

        var sla = new SlaInstance(
            context.Tenant,
            definition.Key,
            target,
            now,
            schedule.DueOnUtc,
            definition.Policy.Kind,
            schedule.Reminders,
            schedule.Escalations,
            schedule.TimeoutOnUtc,
            definition.Policy.CalendarKey);

        if (definition.IsStaged)
        {
            var first = definition.Stages[0];
            sla.AddStage(new SlaStageState(first, now, _scheduler.StageDue(calendar, now, first.Duration)));
        }

        _store.Save(sla);
        Record(sla, SlaHistoryAction.Started, context.StartedBy ?? "system", definition.Name, now);
        Publish(new SlaStarted(sla.Id, sla.Tenant, now, sla.DefinitionKey, sla.Target, sla.DueOnUtc));
        _metrics.RecordStarted();

        return Task.FromResult(sla);
    }

    /// <summary>Stops an SLA's clock.</summary>
    /// <param name="slaId">The SLA id.</param>
    /// <param name="reason">Why the clock is stopping.</param>
    /// <param name="actor">Who is stopping it.</param>
    /// <returns>The paused SLA, or <see langword="null"/> when unknown, not running, or pausing is not allowed.</returns>
    public SlaInstance? Pause(Guid slaId, PauseReason? reason = null, string? actor = null)
    {
        var sla = _store.Get(slaId);
        if (sla is null)
        {
            return null;
        }

        var definition = _definitions.Get(sla.DefinitionKey);
        if (definition is not null && !definition.Policy.AllowPause)
        {
            return null;
        }

        var now = _clock.UtcNow;
        if (!sla.Pause(now, reason))
        {
            return null;
        }

        _store.Save(sla);
        Record(sla, SlaHistoryAction.Paused, actor ?? "system", sla.PausedBecause?.Code, now);
        Publish(new SlaPaused(sla.Id, sla.Tenant, now, sla.DefinitionKey, sla.Target, sla.PausedBecause!));
        _metrics.RecordPaused();
        return sla;
    }

    /// <summary>
    /// Restarts an SLA's clock, shifting every remaining scheduled time forward by the business time the pause
    /// consumed, so a suspension never eats into the budget.
    /// </summary>
    /// <param name="slaId">The SLA id.</param>
    /// <param name="reason">Why the clock is restarting.</param>
    /// <param name="actor">Who is restarting it.</param>
    /// <returns>The resumed SLA, or <see langword="null"/> when unknown or not paused.</returns>
    public SlaInstance? Resume(Guid slaId, ResumeReason? reason = null, string? actor = null)
    {
        var sla = _store.Get(slaId);
        if (sla is null || sla.Status != SlaStatus.Paused || sla.PausedOnUtc is not { } pausedOn)
        {
            return null;
        }

        var now = _clock.UtcNow;
        var calendar = _calendars.Resolve(sla);
        var pausedBusinessTime = _scheduler.PausedBusinessTime(calendar, pausedOn, now);

        if (!sla.Resume(reason, _scheduler.ShiftBy(calendar, pausedBusinessTime)))
        {
            return null;
        }

        _store.Save(sla);
        Record(sla, SlaHistoryAction.Resumed, actor ?? "system", $"+{pausedBusinessTime}", now);
        Publish(new SlaResumed(sla.Id, sla.Tenant, now, sla.DefinitionKey, sla.Target, sla.ResumedBecause!, sla.DueOnUtc));
        _metrics.RecordResumed();
        return sla;
    }

    /// <summary>Advances a staged SLA to its next stage.</summary>
    /// <param name="slaId">The SLA id.</param>
    /// <param name="actor">Who is advancing it.</param>
    /// <returns>The updated SLA, or <see langword="null"/> when unknown, unstaged or finished.</returns>
    public SlaInstance? AdvanceStage(Guid slaId, string? actor = null)
    {
        var sla = _store.Get(slaId);
        var definition = sla is null ? null : _definitions.Get(sla.DefinitionKey);
        if (sla is null || definition is null || !definition.IsStaged || sla.CurrentStage is not { } current)
        {
            return null;
        }

        var now = _clock.UtcNow;
        var calendar = _calendars.Resolve(sla);
        var next = definition.Stages.FirstOrDefault(stage => stage.Order == current.Stage.Order + 1);
        var nextState = next is null
            ? null
            : new SlaStageState(next, now, _scheduler.StageDue(calendar, now, next.Duration));

        if (!sla.AdvanceStage(now, nextState))
        {
            return null;
        }

        _store.Save(sla);
        Record(sla, SlaHistoryAction.StageAdvanced, actor ?? "system", next?.Key ?? current.Stage.Key, now);
        return sla;
    }

    /// <summary>Closes an SLA because the tracked work finished.</summary>
    /// <param name="slaId">The SLA id.</param>
    /// <param name="actor">Who is closing it.</param>
    /// <returns>The completed SLA, or <see langword="null"/> when unknown or already finished.</returns>
    public SlaInstance? Complete(Guid slaId, string? actor = null)
    {
        var sla = _store.Get(slaId);
        var now = _clock.UtcNow;
        if (sla is null || !sla.Complete(now))
        {
            return null;
        }

        _store.Save(sla);
        Record(sla, SlaHistoryAction.Completed, actor ?? "system", sla.Outcome.ToString(), now);
        Publish(new SlaCompleted(sla.Id, sla.Tenant, now, sla.DefinitionKey, sla.Target, sla.Outcome));
        if (sla.Outcome == SlaOutcome.Met)
        {
            _metrics.RecordMet();
        }

        return sla;
    }

    /// <summary>Closes the open SLA tracking a target because that work finished.</summary>
    /// <param name="target">The tracked work.</param>
    /// <param name="actor">Who is closing it.</param>
    /// <returns>The completed SLA, or <see langword="null"/> when nothing open tracks the target.</returns>
    public SlaInstance? CompleteForTarget(SlaTarget target, string? actor = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        var sla = _store.ByTarget(target);
        return sla is null ? null : Complete(sla.Id, actor);
    }

    /// <summary>Cancels an SLA before the tracked work finished.</summary>
    /// <param name="slaId">The SLA id.</param>
    /// <param name="actor">Who is cancelling it.</param>
    /// <param name="reason">An optional reason.</param>
    /// <returns>The cancelled SLA, or <see langword="null"/> when unknown or already finished.</returns>
    public SlaInstance? Cancel(Guid slaId, string? actor = null, string? reason = null)
    {
        var sla = _store.Get(slaId);
        var now = _clock.UtcNow;
        if (sla is null || !sla.Cancel(now))
        {
            return null;
        }

        _store.Save(sla);
        Record(sla, SlaHistoryAction.Cancelled, actor ?? "system", reason, now);
        Publish(new SlaCancelled(sla.Id, sla.Tenant, now, sla.DefinitionKey, sla.Target));
        _metrics.RecordCancelled();
        return sla;
    }

    /// <summary>
    /// Runs the due-work pass over the open SLAs: fires reminders, records deadline breaches, applies
    /// escalations and times out the SLAs whose hard limit passed.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the pass.</param>
    /// <returns>A summary of the pass.</returns>
    public Task<SlaDueWorkSummary> RunDueAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var open = _store.ListOpen().Take(_options.DueWorkBatchSize).ToArray();

        int reminders = 0, breaches = 0, escalations = 0, timedOut = 0;
        foreach (var sla in open)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var due = _evaluator.Evaluate(sla, now);
            if (!due.Any)
            {
                continue;
            }

            foreach (var reminder in due.Reminders)
            {
                reminder.MarkFired();
                Record(sla, SlaHistoryAction.ReminderTriggered, "sla", $"-{reminder.Before}", now);
                Publish(new SlaReminderTriggered(
                    sla.Id, sla.Tenant, now, sla.DefinitionKey, sla.Target, reminder.Before));
                _metrics.RecordReminder();
                reminders++;
            }

            // A missed deadline is recorded but does not end the SLA — escalation continues from here.
            if (due.Breached && sla.Breach(now))
            {
                Record(sla, SlaHistoryAction.Expired, "sla", null, now);
                Publish(new SlaExpired(sla.Id, sla.Tenant, now, sla.DefinitionKey, sla.Target, sla.DueOnUtc));
                _metrics.RecordBreached();
                breaches++;
            }

            foreach (var escalation in due.Escalations)
            {
                escalation.MarkFired();
                sla.RecordEscalation(escalation.Escalation.Level);
                Record(sla, SlaHistoryAction.Escalated, "sla", escalation.Escalation.Assignee, now);
                Publish(new SlaEscalated(
                    sla.Id, sla.Tenant, now, sla.DefinitionKey, sla.Target,
                    escalation.Escalation.Level, escalation.Escalation.Assignee));
                _metrics.RecordEscalation();
                escalations++;
            }

            // The hard timeout is terminal: the SLA stops waiting for the work.
            if (due.TimedOut && _options.TimeOutOverdueSlas && sla.TimeOut(now))
            {
                Record(sla, SlaHistoryAction.TimedOut, "sla", null, now);
                Publish(new SlaTimedOut(sla.Id, sla.Tenant, now, sla.DefinitionKey, sla.Target));
                _metrics.RecordTimedOut();
                timedOut++;
            }

            _store.Save(sla);
        }

        return Task.FromResult(new SlaDueWorkSummary(open.Length, reminders, breaches, escalations, timedOut));
    }

    /// <summary>Gets an SLA by id.</summary>
    /// <param name="slaId">The SLA id.</param>
    /// <returns>The SLA, or <see langword="null"/> when not found.</returns>
    public SlaInstance? Get(Guid slaId) => _store.Get(slaId);

    /// <summary>Gets the open SLA tracking a target, if there is one.</summary>
    /// <param name="target">The tracked work.</param>
    /// <returns>The SLA, or <see langword="null"/>.</returns>
    public SlaInstance? ByTarget(SlaTarget target) => _store.ByTarget(target);

    /// <summary>Lists every SLA that has not finished.</summary>
    /// <returns>The open SLAs.</returns>
    public IReadOnlyCollection<SlaInstance> ListOpen() => _store.ListOpen();

    /// <summary>Gets the history entries of an SLA, oldest first.</summary>
    /// <param name="slaId">The SLA id.</param>
    /// <returns>The entries.</returns>
    public IReadOnlyList<SlaHistoryEntry> GetHistory(Guid slaId) => _history.BySla(slaId);

    private void Publish(SlaEvent slaEvent)
    {
        foreach (var sink in _events)
        {
            sink.Publish(slaEvent);
        }
    }

    private void Record(
        SlaInstance sla, SlaHistoryAction action, string actor, string? detail, DateTimeOffset now) =>
        _history.Append(new SlaHistoryEntry(sla.Id, action, actor, detail, now));
}
