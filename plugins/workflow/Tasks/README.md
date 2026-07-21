# Human Task Engine (Commit 0014)

A **user-task runtime** for FactoryOS: it turns a workflow activity into an assignable, trackable task that
a person completes, rejects or cancels — with assignment resolution, deadlines, reminders, escalation,
permissions, comments, attachments and a full audit history.

> **Where it lives.** Per Commit 0014's rules, **no new project was created.** The engine lives inside the
> existing `FactoryOS.Plugins.Workflow` project under `Tasks/`, in the `FactoryOS.Plugins.Workflow.Tasks.*`
> namespace — beside the workflow process runtime (`…Workflow.Engine.*`) and the forms engine
> (`…Forms.Engine.*`). The reactive workflow rule engine, the workflow process runtime **and** the forms
> engine are all **untouched**; the only link is a one-way bridge (`IHumanTaskWorkflowBridge`) that calls the
> existing `WorkflowEngine.CompleteActivityAsync` / `CancelAsync`.

> **The task engine knows nothing but tasks.** It creates, assigns and tracks tasks and emits lifecycle
> **events** (`HumanTaskCreated`/`Completed`/`Rejected`/…). It has **no knowledge of forms, notifications,
> inboxes, calendars or SLAs** — no bridge to any of them. The **application / orchestration layer** subscribes
> to the task events and drives those engines:
>
> ```
> Workflow Runtime → Activity → Human Task → HumanTaskCreated event
>                                                   │
>                                        Orchestrator / Application Service
>                                        ├──► Forms Engine        (opens metadata["form"], if any)
>                                        ├──► Notification Engine
>                                        ├──► Inbox / Calendar / SLA …
>                                                   │
>                                        (user acts) → Complete/Reject → bridge → Workflow continues
> ```
>
> Anything task-specific the orchestrator needs travels as **opaque `Metadata`** on the definition (e.g. a
> `"form"` entry); the task engine never interprets it. This keeps the task engine from slowly turning into an
> orchestrator as new channels (Teams, mobile, mail, …) are added.

## Model

- **Definition** — `HumanTaskDefinition` (built by `HumanTaskDefinition.Create(key, name, assignment)…Build()`):
  category, priority, assignment strategy, permission grants, an optional deadline with reminders and
  escalations, opaque orchestration `Metadata`, and the optional workflow `ActivityKey` it satisfies.
- **Assignment** — `HumanTaskAssignment`: `ToUser`, `ToRole`, `ToGroup`, `ToExpression` (dynamic),
  `RoundRobin` and `LoadBalanced`. Resolved by `AssignmentResolver` against an `IHumanTaskDirectory`
  (roles/groups → members) and the task store (load-balancing).
- **Instance** — `HumanTaskInstance`: status (`Created → Waiting → InProgress → Completed/Rejected/Cancelled/
  Expired`, plus `Escalated`), assignee and candidates, comments, attachment references, decision, deadline,
  scheduled reminders and escalations, workflow link, and audit history.

## Flow

```
workflow pauses on Activity("approve")
        │
        ▼
HumanTaskEngine.CreateForActivityAsync(def, ctx, workflowInstanceId, "approve")   → Created + Assigned (Waiting)
        │  OpenAsync(...)                                                          → Opened (InProgress)
        ▼
CompleteAsync / ApproveAsync   → Completed → bridge.CompleteActivityAsync(outcome) → workflow advances
RejectAsync                    → Rejected  → bridge.CompleteActivityAsync(approved=false) → workflow branches
CancelAsync                    → Cancelled → bridge.CancelActivityAsync() → workflow instance cancelled

RunDueAsync()  (a scheduler tick):  fires due reminders · applies due escalations · expires overdue tasks
```

## Usage

```csharp
services.AddHumanTaskEngine();          // also registers the workflow engine (idempotently)
// or: services.AddHumanTaskEngine(configuration);  // binds Workflow:Tasks

var def = HumanTaskDefinition.Create("approve-task", "Approve", HumanTaskAssignment.ToUser("manager"))
    .ForActivity("approve")
    .OfCategory(HumanTaskCategory.Approval)
    .WithDeadline(HumanTaskDeadline.In(TimeSpan.FromHours(24)))
    .AddReminder(new HumanTaskReminder(TimeSpan.FromHours(4)))
    .AddEscalation(new HumanTaskEscalation(TimeSpan.FromHours(1), HumanTaskAssignment.ToUser("director")))
    .Build();

var task = await taskEngine.CreateForActivityAsync(def, HumanTaskContext.Default, workflowInstanceId, "approve");
await taskEngine.OpenAsync(task.Id, "manager");
await taskEngine.ApproveAsync(task.Id, "manager");   // the workflow's "approve" activity completes

// periodically, from a scheduler:
await taskEngine.RunDueAsync();   // reminders → escalations → expiries
```

## Notes

- **Complete and Reject both advance** a workflow-bound task's activity (reject passes `approved = false` so a
  downstream decision node can branch); **Cancel** cancels the owning workflow instance.
- **Escalation gives a new lease** — an escalated task is reassigned and does not auto-expire; only tasks with
  no escalation left expire.
- **Permissions** — the assignee implicitly holds read/write/complete/reject; other rights come from the
  definition's grants (`HumanTaskPermissionEvaluator`).
- **Attachments are references only** — the engine stores an object-storage key and metadata, never bytes.
- **Persistence** defaults to in-memory (`IHumanTaskRepository`, `IHumanTaskStore`,
  `IHumanTaskHistoryRepository`); **events** go through `IHumanTaskEventSink` (the event-bus seam).
- **No secrets** — `sample.config.json` binds `Workflow:Tasks` only.

Out of scope for this commit (by the spec): the human-task UI, forms designer and notification delivery.
