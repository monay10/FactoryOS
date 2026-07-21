using FactoryOS.Plugins.Workflow.SLA.Configuration;
using FactoryOS.Plugins.Workflow.SLA.Diagnostics;
using FactoryOS.Plugins.Workflow.SLA.Domain;
using FactoryOS.Plugins.Workflow.SLA.Events;
using FactoryOS.Plugins.Workflow.SLA.Execution;
using FactoryOS.Plugins.Workflow.SLA.Persistence;
using FactoryOS.Tests.Identity;

namespace FactoryOS.Tests.Workflow.Sla;

/// <summary>
/// Unit coverage of the SLA engine core: business-time arithmetic over working hours, weekends, holidays and
/// shift breaks; deadlines, reminders, escalations and hard timeouts; pausing and resuming the clock; staged
/// SLAs; permissions and history — exercised directly, without a container and without any of the engines whose
/// work an SLA tracks.
/// </summary>
public sealed class SlaEngineCoreTests
{
    // 2026-07-20 is a Monday; every date below is chosen relative to it so the weekday maths is explicit.
    private static readonly DateTimeOffset MondayNine = new(2026, 07, 20, 09, 00, 00, TimeSpan.Zero);
    private static readonly DateTimeOffset FridayThree = new(2026, 07, 24, 15, 00, 00, TimeSpan.Zero);

    private static readonly TimeOnly NineAm = new(09, 00);
    private static readonly TimeOnly FivePm = new(17, 00);

    private static BusinessCalendar WeekdayCalendar() =>
        new BusinessCalendar("factory").AddWeekdays(NineAm, FivePm);

    private static SlaCalendar WeekdaySlaCalendar() => SlaCalendar.Of(WeekdayCalendar());

    // ---- Business time: working hours ------------------------------------------------------------------------

    [Fact]
    public void Business_time_within_one_working_day_is_plain_arithmetic()
    {
        var calculator = new BusinessTimeCalculator();

        var due = calculator.Add(WeekdaySlaCalendar(), MondayNine, TimeSpan.FromHours(4));

        Assert.Equal(new DateTimeOffset(2026, 07, 20, 13, 00, 00, TimeSpan.Zero), due);
    }

    [Fact]
    public void Business_time_spills_into_the_next_working_day()
    {
        var calculator = new BusinessTimeCalculator();
        var mondayThree = new DateTimeOffset(2026, 07, 20, 15, 00, 00, TimeSpan.Zero);

        // Two hours remain on Monday; the other two are served from Tuesday morning.
        var due = calculator.Add(WeekdaySlaCalendar(), mondayThree, TimeSpan.FromHours(4));

        Assert.Equal(new DateTimeOffset(2026, 07, 21, 11, 00, 00, TimeSpan.Zero), due);
    }

    [Fact]
    public void Business_time_skips_the_weekend()
    {
        var calculator = new BusinessTimeCalculator();

        // Friday 15:00 + 4 working hours: 2h on Friday, then Saturday and Sunday are skipped entirely.
        var due = calculator.Add(WeekdaySlaCalendar(), FridayThree, TimeSpan.FromHours(4));

        Assert.Equal(new DateTimeOffset(2026, 07, 27, 11, 00, 00, TimeSpan.Zero), due);
        Assert.Equal(DayOfWeek.Monday, due.DayOfWeek);
    }

    [Fact]
    public void Work_that_starts_before_opening_time_waits_for_the_window_to_open()
    {
        var calculator = new BusinessTimeCalculator();
        var mondaySeven = new DateTimeOffset(2026, 07, 20, 07, 00, 00, TimeSpan.Zero);

        var due = calculator.Add(WeekdaySlaCalendar(), mondaySeven, TimeSpan.FromHours(1));

        Assert.Equal(new DateTimeOffset(2026, 07, 20, 10, 00, 00, TimeSpan.Zero), due);
    }

    [Fact]
    public void Work_that_starts_after_closing_time_rolls_to_the_next_day()
    {
        var calculator = new BusinessTimeCalculator();
        var mondayEvening = new DateTimeOffset(2026, 07, 20, 18, 00, 00, TimeSpan.Zero);

        var due = calculator.Add(WeekdaySlaCalendar(), mondayEvening, TimeSpan.FromHours(1));

        Assert.Equal(new DateTimeOffset(2026, 07, 21, 10, 00, 00, TimeSpan.Zero), due);
    }

    [Fact]
    public void Business_time_skips_a_shift_break()
    {
        var calculator = new BusinessTimeCalculator();
        var calendar = SlaCalendar.Of(new BusinessCalendar("shifts")
            .AddWorkingHours(WorkingHours.Of(DayOfWeek.Monday, NineAm, new TimeOnly(12, 00)))
            .AddWorkingHours(WorkingHours.Of(DayOfWeek.Monday, new TimeOnly(13, 00), FivePm)));
        var mondayEleven = new DateTimeOffset(2026, 07, 20, 11, 00, 00, TimeSpan.Zero);

        // One hour before the break, one after it — the break itself does not count.
        var due = calculator.Add(calendar, mondayEleven, TimeSpan.FromHours(2));

        Assert.Equal(new DateTimeOffset(2026, 07, 20, 14, 00, 00, TimeSpan.Zero), due);
    }

    // ---- Business time: holidays -----------------------------------------------------------------------------

    [Fact]
    public void A_holiday_is_skipped_entirely()
    {
        var calculator = new BusinessTimeCalculator();
        var holidays = new HolidayCalendar("tr").Add(new DateOnly(2026, 07, 21)); // the Tuesday
        var calendar = SlaCalendar.Of(new BusinessCalendar("factory", holidays: holidays).AddWeekdays(NineAm, FivePm));
        var mondayThree = new DateTimeOffset(2026, 07, 20, 15, 00, 00, TimeSpan.Zero);

        var due = calculator.Add(calendar, mondayThree, TimeSpan.FromHours(4));

        Assert.Equal(new DateTimeOffset(2026, 07, 22, 11, 00, 00, TimeSpan.Zero), due);
    }

    [Fact]
    public void Elapsed_business_time_counts_only_working_hours()
    {
        var calculator = new BusinessTimeCalculator();
        var mondayFour = new DateTimeOffset(2026, 07, 20, 16, 00, 00, TimeSpan.Zero);
        var tuesdayTen = new DateTimeOffset(2026, 07, 21, 10, 00, 00, TimeSpan.Zero);

        // One hour on Monday plus one on Tuesday; the night in between does not count.
        Assert.Equal(TimeSpan.FromHours(2), calculator.Elapsed(WeekdaySlaCalendar(), mondayFour, tuesdayTen));
    }

    [Fact]
    public void A_continuous_calendar_counts_every_hour()
    {
        var calculator = new BusinessTimeCalculator();

        Assert.Equal(
            FridayThree.AddHours(48),
            calculator.Add(SlaCalendar.Continuous, FridayThree, TimeSpan.FromHours(48)));
        Assert.Equal(
            TimeSpan.FromHours(48),
            calculator.Elapsed(SlaCalendar.Continuous, FridayThree, FridayThree.AddHours(48)));
    }

    [Fact]
    public void A_calendar_with_no_working_window_is_a_configuration_error()
    {
        var calculator = new BusinessTimeCalculator();
        var empty = SlaCalendar.Of(new BusinessCalendar("empty"));

        Assert.Throws<InvalidOperationException>(() =>
            calculator.Add(empty, MondayNine, TimeSpan.FromHours(1)));
    }

    // ---- Deadlines ------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_deadline_is_measured_in_business_time()
    {
        var harness = Harness.Create(new DateTimeOffset(2026, 07, 20, 15, 00, 00, TimeSpan.Zero));
        harness.Engine.RegisterCalendar(WeekdayCalendar());

        var sla = await harness.Engine.StartAsync(
            SlaDefinition.Create("repair", "Repair")
                .Using(SlaPolicy.WorkingHours("factory"))
                .WithDeadline(TimeSpan.FromHours(8))
                .Build(),
            SlaTarget.ForHumanTask("fix", Guid.NewGuid()),
            SlaContext.Default);

        // Two hours on Monday, six on Tuesday.
        Assert.Equal(new DateTimeOffset(2026, 07, 21, 15, 00, 00, TimeSpan.Zero), sla.DueOnUtc);
    }

    [Fact]
    public async Task A_missed_deadline_breaches_the_sla_but_does_not_end_it()
    {
        var harness = Harness.Create(MondayNine);
        var sla = await harness.StartSimpleAsync(TimeSpan.FromHours(2));

        harness.Clock.Advance(TimeSpan.FromHours(3));
        var pass = await harness.Engine.RunDueAsync();

        Assert.Equal(1, pass.Breaches);
        var breached = harness.Engine.GetSla(sla.Id)!;
        Assert.Equal(SlaStatus.Breached, breached.Status);
        Assert.False(breached.IsTerminal);
        Assert.Equal(SlaOutcome.None, breached.Outcome);
        Assert.Contains(harness.Events.Events, e => e is SlaExpired);
    }

    [Fact]
    public async Task Finishing_inside_the_deadline_is_met_and_outside_it_is_breached()
    {
        var onTime = Harness.Create(MondayNine);
        var early = await onTime.StartSimpleAsync(TimeSpan.FromHours(2));
        onTime.Clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal(SlaOutcome.Met, onTime.Engine.Complete(early.Id)!.Outcome);

        var late = Harness.Create(MondayNine);
        var overdue = await late.StartSimpleAsync(TimeSpan.FromHours(2));
        late.Clock.Advance(TimeSpan.FromHours(3));
        Assert.Equal(SlaOutcome.Breached, late.Engine.Complete(overdue.Id)!.Outcome);
    }

    // ---- Reminders ------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_reminder_fires_once_ahead_of_the_deadline()
    {
        var harness = Harness.Create(MondayNine);
        var sla = await harness.Engine.StartAsync(
            SlaDefinition.Create("respond", "Respond")
                .WithDeadline(TimeSpan.FromHours(2))
                .AddReminder(TimeSpan.FromMinutes(30))
                .Build(),
            SlaTarget.ForApproval("spend", Guid.NewGuid()),
            SlaContext.Default);

        Assert.Equal(0, (await harness.Engine.RunDueAsync()).Reminders);

        harness.Clock.Advance(TimeSpan.FromMinutes(95));
        Assert.Equal(1, (await harness.Engine.RunDueAsync()).Reminders);

        // A reminder never fires twice.
        Assert.Equal(0, (await harness.Engine.RunDueAsync()).Reminders);
        Assert.Single(harness.Events.Events, e => e is SlaReminderTriggered);
        Assert.True(harness.Engine.GetSla(sla.Id)!.Reminders[0].Fired);
    }

    // ---- Escalation -----------------------------------------------------------------------------------------

    [Fact]
    public async Task An_escalation_fires_after_the_deadline_and_names_its_assignee()
    {
        var harness = Harness.Create(MondayNine);
        var sla = await harness.Engine.StartAsync(
            SlaDefinition.Create("repair", "Repair")
                .WithDeadline(TimeSpan.FromHours(2))
                .AddEscalation(TimeSpan.FromMinutes(30), "u-supervisor")
                .Build(),
            SlaTarget.ForHumanTask("fix", Guid.NewGuid()),
            SlaContext.Default);

        harness.Clock.Advance(TimeSpan.FromHours(3));
        var pass = await harness.Engine.RunDueAsync();

        Assert.Equal(1, pass.Escalations);
        Assert.Equal(1, harness.Engine.GetSla(sla.Id)!.EscalationLevel);
        var escalated = Assert.Single(harness.Events.Events.OfType<SlaEscalated>());
        Assert.Equal("u-supervisor", escalated.Assignee);
    }

    // ---- Timeout, and its separation from expiry -------------------------------------------------------------

    [Fact]
    public async Task A_hard_timeout_ends_the_sla()
    {
        var harness = Harness.Create(MondayNine);
        var sla = await harness.Engine.StartAsync(
            SlaDefinition.Create("repair", "Repair")
                .WithDeadline(TimeSpan.FromHours(2))
                .WithTimeout(TimeSpan.FromHours(1))
                .Build(),
            SlaTarget.ForHumanTask("fix", Guid.NewGuid()),
            SlaContext.Default);

        harness.Clock.Advance(TimeSpan.FromHours(4));
        var pass = await harness.Engine.RunDueAsync();

        Assert.Equal(1, pass.TimedOut);
        var timedOut = harness.Engine.GetSla(sla.Id)!;
        Assert.Equal(SlaStatus.TimedOut, timedOut.Status);
        Assert.True(timedOut.IsTerminal);
        Assert.Contains(harness.Events.Events, e => e is SlaTimedOut);
    }

    [Fact]
    public async Task Expired_and_timed_out_stay_distinct_dispositions()
    {
        var harness = Harness.Create(MondayNine);
        var sla = await harness.Engine.StartAsync(
            SlaDefinition.Create("repair", "Repair")
                .WithDeadline(TimeSpan.FromHours(2))
                .WithTimeout(TimeSpan.FromHours(2))
                .Build(),
            SlaTarget.ForHumanTask("fix", Guid.NewGuid()),
            SlaContext.Default);

        // Past the deadline but not yet the timeout: breached, still running.
        harness.Clock.Advance(TimeSpan.FromHours(3));
        await harness.Engine.RunDueAsync();
        Assert.Equal(SlaStatus.Breached, harness.Engine.GetSla(sla.Id)!.Status);
        Assert.Contains(harness.Events.Events, e => e is SlaExpired);
        Assert.DoesNotContain(harness.Events.Events, e => e is SlaTimedOut);

        // Past the timeout as well: now it gives up, and the outcome is TimedOut — not merely Breached.
        harness.Clock.Advance(TimeSpan.FromHours(2));
        await harness.Engine.RunDueAsync();

        var finished = harness.Engine.GetSla(sla.Id)!;
        Assert.Equal(SlaStatus.TimedOut, finished.Status);
        Assert.Equal(SlaOutcome.TimedOut, finished.Outcome);
        Assert.Contains(harness.Events.Events, e => e is SlaExpired);
        Assert.Contains(harness.Events.Events, e => e is SlaTimedOut);
    }

    // ---- Pause / resume -------------------------------------------------------------------------------------

    [Fact]
    public async Task A_paused_clock_owes_nothing_and_resuming_shifts_the_deadline()
    {
        var harness = Harness.Create(MondayNine);
        harness.Engine.RegisterCalendar(WeekdayCalendar());
        var sla = await harness.Engine.StartAsync(
            SlaDefinition.Create("repair", "Repair")
                .Using(SlaPolicy.WorkingHours("factory"))
                .WithDeadline(TimeSpan.FromHours(8))
                .Build(),
            SlaTarget.ForHumanTask("fix", Guid.NewGuid()),
            SlaContext.Default);
        Assert.Equal(new DateTimeOffset(2026, 07, 20, 17, 00, 00, TimeSpan.Zero), sla.DueOnUtc);

        harness.Clock.Advance(TimeSpan.FromHours(1));
        Assert.NotNull(harness.Engine.Pause(sla.Id, new PauseReason("waiting-on-parts")));

        // While paused the clock owes nothing, even once the original deadline passes.
        harness.Clock.Advance(TimeSpan.FromHours(9));
        var pass = await harness.Engine.RunDueAsync();
        Assert.Equal(0, pass.Breaches);
        Assert.Equal(SlaStatus.Paused, harness.Engine.GetSla(sla.Id)!.Status);

        var resumed = harness.Engine.Resume(sla.Id, new ResumeReason("parts-arrived"))!;

        // The pause consumed 7 working hours (10:00→17:00), so the deadline moves that far forward.
        Assert.Equal(SlaStatus.Active, resumed.Status);
        Assert.Equal(new DateTimeOffset(2026, 07, 21, 16, 00, 00, TimeSpan.Zero), resumed.DueOnUtc);
        Assert.Contains(harness.Events.Events, e => e is SlaPaused);
        Assert.Contains(harness.Events.Events, e => e is SlaResumed);
    }

    [Fact]
    public async Task A_policy_can_forbid_stopping_the_clock()
    {
        var harness = Harness.Create(MondayNine);
        var sla = await harness.Engine.StartAsync(
            SlaDefinition.Create("hard", "Hard")
                .Using(new SlaPolicy(SlaCalendarKind.Continuous, allowPause: false))
                .WithDeadline(TimeSpan.FromHours(2))
                .Build(),
            SlaTarget.ForApproval("spend", Guid.NewGuid()),
            SlaContext.Default);

        Assert.Null(harness.Engine.Pause(sla.Id));
        Assert.Equal(SlaStatus.Active, harness.Engine.GetSla(sla.Id)!.Status);
    }

    // ---- Stages ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_staged_sla_gives_each_stage_its_own_deadline()
    {
        var harness = Harness.Create(MondayNine);
        var sla = await harness.Engine.StartAsync(
            SlaDefinition.Create("incident", "Incident")
                .AddStage("triage", "Triage", TimeSpan.FromHours(1))
                .AddStage("repair", "Repair", TimeSpan.FromHours(4))
                .Build(),
            SlaTarget.ForWorkflowActivity("handle", Guid.NewGuid()),
            SlaContext.Default);

        // The overall budget defaults to the sum of the stages.
        Assert.Equal(MondayNine.AddHours(5), sla.DueOnUtc);
        Assert.Equal("triage", sla.CurrentStage!.Stage.Key);
        Assert.Equal(MondayNine.AddHours(1), sla.CurrentStage.DueOnUtc);

        harness.Clock.Advance(TimeSpan.FromMinutes(30));
        var advanced = harness.Engine.AdvanceStage(sla.Id)!;

        Assert.Equal("repair", advanced.CurrentStage!.Stage.Key);
        Assert.Equal(MondayNine.AddMinutes(30).AddHours(4), advanced.CurrentStage.DueOnUtc);
        Assert.True(advanced.Stages[0].Met);
    }

    // ---- Cancel, permissions, history -----------------------------------------------------------------------

    [Fact]
    public async Task A_cancelled_sla_finishes_as_cancelled()
    {
        var harness = Harness.Create(MondayNine);
        var sla = await harness.StartSimpleAsync(TimeSpan.FromHours(2));

        var cancelled = harness.Engine.Cancel(sla.Id, "admin", "work withdrawn")!;

        Assert.Equal(SlaStatus.Cancelled, cancelled.Status);
        Assert.Equal(SlaOutcome.Cancelled, cancelled.Outcome);
        Assert.Contains(harness.Events.Events, e => e is SlaCancelled);
    }

    [Fact]
    public async Task Permissions_come_from_the_definitions_grants()
    {
        var harness = Harness.Create(MondayNine);
        var definition = SlaDefinition.Create("repair", "Repair")
            .WithDeadline(TimeSpan.FromHours(2))
            .Grant("role:supervisor", SlaPermission.View | SlaPermission.Override)
            .Build();
        var sla = await harness.Engine.StartAsync(
            definition, SlaTarget.ForHumanTask("fix", Guid.NewGuid()), new SlaContext("default", "u-alice"));

        Assert.Equal(
            SlaPermission.View | SlaPermission.Override,
            harness.Engine.PermissionsFor(definition, sla, "role:supervisor"));
        Assert.Equal(SlaPermission.View, harness.Engine.PermissionsFor(definition, sla, "u-alice", "u-alice"));
        Assert.Equal(SlaPermission.None, harness.Engine.PermissionsFor(definition, sla, "u-stranger"));
    }

    [Fact]
    public async Task The_history_records_the_lifecycle()
    {
        var harness = Harness.Create(MondayNine);
        var sla = await harness.StartSimpleAsync(TimeSpan.FromHours(2));
        harness.Clock.Advance(TimeSpan.FromMinutes(30));
        harness.Engine.Pause(sla.Id, new PauseReason("waiting"));
        harness.Clock.Advance(TimeSpan.FromMinutes(30));
        harness.Engine.Resume(sla.Id, new ResumeReason("ready"));
        harness.Engine.Complete(sla.Id, "u-alice");

        var actions = harness.Engine.GetHistory(sla.Id).Select(entry => entry.Action).ToArray();

        Assert.Contains(SlaHistoryAction.Started, actions);
        Assert.Contains(SlaHistoryAction.Paused, actions);
        Assert.Contains(SlaHistoryAction.Resumed, actions);
        Assert.Contains(SlaHistoryAction.Completed, actions);
    }

    [Fact]
    public async Task An_open_sla_is_findable_by_the_target_it_tracks()
    {
        var harness = Harness.Create(MondayNine);
        var target = SlaTarget.ForApproval("spend", Guid.NewGuid());
        var sla = await harness.Engine.StartAsync(
            SlaDefinition.Create("approve", "Approve").WithDeadline(TimeSpan.FromHours(2)).Build(),
            target,
            SlaContext.Default);

        Assert.Equal(sla.Id, harness.Engine.ByTarget(target)!.Id);

        harness.Engine.CompleteForTarget(target, "u-alice");

        Assert.Null(harness.Engine.ByTarget(target));
        Assert.Equal(SlaStatus.Completed, harness.Engine.GetSla(sla.Id)!.Status);
    }

    // ---- Helpers --------------------------------------------------------------------------------------------

    /// <summary>A fully in-memory SLA pipeline wired by hand for unit tests.</summary>
    private sealed class Harness
    {
        private Harness(SlaEngine engine, InMemorySlaEventSink events, MutableClock clock)
        {
            Engine = engine;
            Events = events;
            Clock = clock;
        }

        public SlaEngine Engine { get; }

        public InMemorySlaEventSink Events { get; }

        public MutableClock Clock { get; }

        public static Harness Create(DateTimeOffset now)
        {
            var clock = new MutableClock(now);
            var events = new InMemorySlaEventSink();
            var store = new InMemorySlaStore();
            var history = new InMemorySlaHistoryRepository();
            var definitions = new InMemorySlaRepository();
            var calendars = new InMemorySlaCalendarRepository();
            var metrics = new SlaMetrics();
            var options = new SlaEngineOptions();

            var calculator = new BusinessTimeCalculator();
            var scheduler = new SlaScheduler(calculator);
            var calendarEngine = new CalendarEngine(calendars);
            var evaluator = new SlaEvaluator(
                new DeadlineEngine(), new ReminderEngine(), new EscalationEngine(), new TimeoutEngine());
            var runtime = new SlaRuntime(
                scheduler, calendarEngine, evaluator, definitions, store, history, [events], metrics, options, clock);
            var engine = new SlaEngine(runtime, calendarEngine, new SlaPermissionEvaluator(), metrics);

            return new Harness(engine, events, clock);
        }

        public Task<SlaInstance> StartSimpleAsync(TimeSpan deadline) => Engine.StartAsync(
            SlaDefinition.Create("simple", "Simple").WithDeadline(deadline).Build(),
            SlaTarget.ForHumanTask("fix", Guid.NewGuid()),
            SlaContext.Default);
    }
}
