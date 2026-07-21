# SLA Engine (Commit 0017)

The shared **service-level-agreement** infrastructure for FactoryOS: it puts a business-time clock on a piece of
work — a workflow activity, a human task, an approval or a form submission — warns before the deadline,
records a breach when it passes, escalates, and hard-times-out when the wait becomes pointless. Working hours,
holidays, shift breaks and time zones are configuration; pausing the clock gives the time back.

> **Where it lives.** Per Commit 0017's rules, **no new project was created.** The engine lives inside the
> existing `FactoryOS.Plugins.Workflow` project under `SLA/`, in the `FactoryOS.Plugins.Workflow.SLA.*`
> namespace — beside the workflow runtime, the forms, human task, approval and notification engines.

## An SLA is attached, not inferred

The engine holds only an `SlaTarget` reference to the work. It does **not** subscribe to the workflow, human
task, approval or forms engines, does not call into them, and does not modify them. The orchestration layer
starts the SLA and tells it when the work finished:

```
orchestration layer
   │  StartAsync(definition, SlaTarget.ForHumanTask("inspect", taskId), context)
   ▼
SlaEngine ──► SlaRuntime ──► RunDueAsync() ──► reminders · breach · escalation · timeout
   ▲                              │
   │  CompleteForTarget(target)   └──► SlaEvent ──► every registered ISlaEventSink
```

This was a deliberate choice. The single-sink `I*EventSink` seams of the other engines are already claimed by
the notification engine, so a second event consumer cannot be added without changing them — and an SLA is a
contract someone signs up for, not something that should be guessed from an event. Attaching it explicitly also
means one target can have several SLAs (a response SLA and a resolution SLA) without ambiguity.

**The SLA event seam fans out.** Unlike the other engines, `SlaRuntime` publishes to *every* registered
`ISlaEventSink`, so a recorder and a bridge can both observe the stream without displacing each other.

## Deadline vs timeout — why both exist

| | Fires when | Terminal? | Event | Outcome |
|---|---|---|---|---|
| **Deadline** | the business-time budget runs out | **no** — work continues, escalations keep firing | `SlaExpired` | `Breached` once the work finishes |
| **Timeout** | the hard limit after the deadline runs out | **yes** — the SLA stops waiting | `SlaTimedOut` | `TimedOut` |

`SlaStatus` is the lifecycle (`Active`, `Paused`, `Breached`, `TimedOut`, `Completed`, `Cancelled`);
`SlaOutcome` is the terminal disposition kept separate for KPIs (`Met`, `Breached`, `TimedOut`, `Cancelled`).
A late finish, a give-up and a cancellation never collapse into one number.

## Business time

`BusinessTimeCalculator` is the whole of the arithmetic, and everything else goes through it:

- **Working hours** — one or more windows per weekday, so a lunch break or a split shift is expressed directly.
- **Holidays** — a `HolidayCalendar` of dates that are skipped entirely.
- **Time zone** — a fixed UTC offset, so the same input yields the same answer on every host and container.
- **24x7** — a continuous calendar short-circuits to plain arithmetic.

> Friday 15:00 + 4 working hours on a 09:00–17:00 weekday calendar lands on **Monday 11:00**, not Friday 19:00.

**Pause gives the time back.** `Pause` stops the clock (a paused SLA owes nothing, even past its original
deadline); `Resume` measures the business time the pause consumed and shifts the deadline, every unfired
reminder and escalation, the timeout and the open stage forward by exactly that much.

## Usage

```csharp
services.AddSlaEngine();                      // core only — no dependency on notifications
// optional: services.AddSlaNotificationIntegration();   // forwards reminders/escalations/breaches/timeouts

slas.RegisterCalendar(new BusinessCalendar("factory-tr",
        new TimeZoneDefinition("Europe/Istanbul", TimeSpan.FromHours(3)),
        holidays: new HolidayCalendar("tr-public").Add(new DateOnly(2026, 10, 29)))
    .AddWeekdays(new TimeOnly(08, 00), new TimeOnly(17, 00)));

var definition = SlaDefinition.Create("maintenance-response", "Maintenance Response")
    .For(SlaTargetKind.HumanTask)
    .Using(SlaPolicy.WorkingHours("factory-tr"))
    .WithDeadline(TimeSpan.FromHours(4))
    .AddReminder(TimeSpan.FromHours(1))                       // one working hour before the deadline
    .AddEscalation(TimeSpan.FromHours(1), "role:shift-supervisor")
    .AddEscalation(TimeSpan.FromHours(4), "role:plant-manager")
    .WithTimeout(TimeSpan.FromHours(24))                      // give up a working day after the deadline
    .Build();

var sla = await slas.StartAsync(definition, SlaTarget.ForHumanTask("inspect", taskId), new SlaContext("acme"));

await slas.RunDueAsync();                     // a scheduler tick
slas.Pause(sla.Id, new PauseReason("waiting-on-parts"));
slas.Resume(sla.Id, new ResumeReason("parts-arrived"));
slas.CompleteForTarget(SlaTarget.ForHumanTask("inspect", taskId));   // when the task finishes
```

## Notes

- **Staged SLAs** — `AddStage` gives each phase (triage → repair → verify) its own budget; `AdvanceStage`
  closes the current one and starts the next from now. The overall budget defaults to the sum of the stages, so
  the two can never disagree.
- **Escalations name their assignee**, which is what lets the notification bridge deliver to a person rather
  than broadcast.
- **A missing calendar is an error, not a fallback.** A policy naming an unregistered calendar throws instead of
  silently degrading to a 24x7 clock, which would quietly make every deadline wrong.
- **Persistence** defaults to in-memory (`ISlaRepository`, `ISlaStore`, `ISlaHistoryRepository`,
  `ISlaCalendarRepository`); **events** go through `ISlaEventSink` (the event-bus seam).
- **No secrets** — `sample.config.json` binds `Workflow:Sla` only.

Out of scope for this commit (by the spec): the SLA dashboard UI, and automatic attachment of SLAs from engine
events — which is orchestration-layer work, deliberately left there.
