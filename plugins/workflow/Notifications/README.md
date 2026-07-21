# Notification Engine (Commit 0016)

An **enterprise notification infrastructure** for FactoryOS: it listens to the events raised by the workflow
runtime, the human task engine, the approval engine and the forms engine, turns them (and explicit requests)
into per-recipient, per-channel messages, renders them from templates, honours each recipient's preferences and
subscriptions, and delivers them through a queue with retries, back-off and a dead-letter queue — keeping a full
delivery history.

> **Where it lives.** Per Commit 0016's rules, **no new project was created.** The engine lives inside the
> existing `FactoryOS.Plugins.Workflow` project under `Notifications/`, in the
> `FactoryOS.Plugins.Workflow.Notifications.*` namespace — beside the workflow runtime (`…Workflow.Engine.*`),
> the forms engine (`…Forms.Engine.*`), the human task engine (`…Workflow.Tasks.*`) and the approval engine
> (`…Workflow.Approvals.*`).

> **The dependency runs one way, and only through events.** The notification engine references the source
> engines' **event contracts** (`…Engine.Events`, `…Tasks.Events`, `…Approvals.Events`, `…Forms.Engine.Events`)
> and nothing else. **No source engine references the notification engine**, and none of them was modified. This
> is the orchestration-layer composition the engine-layering rule calls for: engines emit events, and a
> subscriber above them composes the reaction.

## How the events reach it

Each source engine publishes through its own `I…EventSink` seam. `AddNotificationEngine()` registers the
integration subscribers **as those sinks** (before composing the engines, so its registrations win), which is
how the events flow in without a single line changing in the engines:

```
WorkflowEngine ──IWorkflowEventSink──►  WorkflowNotificationSubscriber  ─┐
HumanTaskEngine ─IHumanTaskEventSink─►  HumanTaskNotificationSubscriber ─┤
ApprovalEngine ──IApprovalEventSink──►  ApprovalNotificationSubscriber  ─┼─► NotificationEngine.Notify(request)
FormEngine ──────IFormEventSink──────►  FormsNotificationSubscriber     ─┤
any module ──────────────────────────►  GenericEventSubscriber          ─┘
```

| Source event | Becomes |
|---|---|
| `WorkflowCompleted` / `WorkflowFailed` | `Workflow` / `Alert` notification for the category's subscribers |
| `HumanTaskCreated` | `HumanTask` notification for the category's subscribers |
| `HumanTaskAssigned` / `HumanTaskEscalated` | `HumanTask` / `Escalation` notification for the (new) assignee |
| `ApprovalStarted` | `Approval` notification for the category's subscribers |
| `ApprovalAssigned` / `ApprovalEscalated` | `Approval` / `Escalation` notification for the (new) approver |
| `FormSubmitted` / `FormRejected` | `Form` / `Alert` notification for the category's subscribers |

## Pipeline

```
NotificationRequest + NotificationContext
        │
        ▼
NotificationRouter ── resolves recipients (user · role · group · dynamic expression)
        │             ── adds matching subscribers, applies NotificationRules
        │             ── filters channels through NotificationPreference (mute · allow-list · quiet hours)
        │             ── renders the NotificationTemplate per channel and culture
        ▼
NotificationQueue ──► NotificationQueueProcessor ──► NotificationDispatcher ──► channel sender ──► outbox
                                   │                         │
                                   │                         └─ failure ─► NotificationRetryService
                                   │                                          │ budget left → back-off retry
                                   └─ time-to-live passed → Expired            └ exhausted   → DeadLetterQueue
```

- **Channels** — `Email`, `Sms`, `Push`, `Teams`, `Slack`, `Webhook`, `InApp`, `SignalR`. Each sender validates
  the address for its channel (a mailbox, a phone number, an absolute URL, …) and records the formatted message
  to the `INotificationOutbox` — the seam onto the real provider.
- **Delivery policies** — `Immediate` (delivered as produced), `Scheduled` (at a due time), `Delayed` (after a
  delay), `Digest` / `Batch` (held out of the normal queue and folded into one message by `FlushDigestsAsync`).
- **Retry** — `NotificationRetryPolicy(MaxAttempts, Backoff)` with linear back-off; the final failure
  dead-letters the notification and raises `NotificationFailed` with `DeadLettered: true`. Dead letters can be
  inspected and replayed with `RequeueDeadLetter`.
- **Statuses** — `Queued → Sending → Sent → Delivered → Read`, or `Retrying → DeadLettered`, or `Cancelled` /
  `Expired` / `Suppressed`.

## Usage

```csharp
services.AddNotificationEngine();          // also composes the workflow, task, approval and forms engines
// or: services.AddNotificationEngine(configuration);  // binds Workflow:Notifications

notifications.RegisterTemplate(new NotificationTemplate(
    "approval-requested", NotificationChannel.Email,
    body: "Hello {{approver}}, '{{title}}' awaits your decision.",
    subject: "Approval requested: {{title}}"));

notifications.SetPreference(new NotificationPreference("u-alice",
    allowedChannels: [NotificationChannel.InApp, NotificationChannel.Email],
    quietHoursStart: new TimeOnly(22, 0), quietHoursEnd: new TimeOnly(7, 0)));

notifications.Subscribe(new NotificationSubscription("u-ops", NotificationCategory.Workflow));

await notifications.NotifyAsync(
    new NotificationRequest
    {
        Category = NotificationCategory.Approval,
        Channels = [NotificationChannel.Email],
        Recipients = [NotificationAssignment.ToRole("approver")],
        TemplateKey = "approval-requested",
    },
    new NotificationContext("acme", values: new Dictionary<string, object?> { ["title"] = "CAPEX-114" }));

await notifications.ProcessDueAsync();     // a scheduler tick: delivers what is due, retries what failed
```

## Notes

- **`Notify` vs `NotifyAsync`** — `Notify` only produces and queues, so it is safe to call from a synchronous
  event handler (that is what the subscribers use); `NotifyAsync` additionally drains the queue for immediate
  notifications.
- **Preferences suppress, they do not silently drop** — a muted category or a quiet-hours window produces a
  `Suppressed` notification and a `NotificationSuppressed` event, so the decision stays auditable.
  `Critical` priority bypasses mute and quiet hours (but still respects the channel allow-list).
- **Definition defaults merge into a request** — recipients, channels, priority, delivery policy, template and
  retry budget fall back to the producing `NotificationDefinition`; anything explicit on the request wins.
- **Persistence** defaults to in-memory (`INotificationRepository`, `INotificationStore`,
  `INotificationHistoryRepository`, `INotificationTemplateRepository`); **events** go through
  `INotificationEventSink` (the event-bus seam), and people are resolved through `INotificationDirectory`.
- **No secrets** — `sample.config.json` binds `Workflow:Notifications` and refers to provider credentials only
  through `${secret:...}` placeholders.

Out of scope for this commit (by the spec): the notification UI/inbox screens, and real provider transports —
the channel senders are the seam those plug into.
