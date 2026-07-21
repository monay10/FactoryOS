# Approval Engine (Commit 0015)

An **enterprise approval runtime** for FactoryOS: it turns a workflow activity into a multi-stage, multi-party
approval with policies (single, sequential, parallel, majority, consensus, weighted, percentage, first
response), auto-decision rules, deadlines, reminders, escalation, permissions and a full audit history — and
routes the workflow down its approval or rejection branch when the approval finishes.

> **Where it lives.** Per Commit 0015's rules, **no new project was created.** The engine lives inside the
> existing `FactoryOS.Plugins.Workflow` project under `Approvals/`, in the
> `FactoryOS.Plugins.Workflow.Approvals.*` namespace — beside the workflow runtime (`…Workflow.Engine.*`), the
> forms engine (`…Forms.Engine.*`) and the human task engine (`…Workflow.Tasks.*`).

> **The approval engine knows only approvals and the workflow it sits on.** It emits lifecycle **events**
> (`ApprovalCreated`/`Started`/`Approved`/`Rejected`/`Completed`/…) and completes/cancels its workflow activity
> through a one-way bridge. It **does not reference the human task engine or the forms engine** — surfacing an
> approver's step as a human task, or collecting input on a form, is composed by the **orchestration layer**
> that subscribes to the approval events. (See the engine-layering rule established in Commits 0013–0014.)

## Model

- **Definition** — `ApprovalDefinition` (built via `ApprovalDefinition.Create(key, name)…Build()`): ordered
  **stages**, each with an `ApprovalPolicy` and its `ApprovalParticipant`s; optional auto-decision `ApprovalRule`s;
  permission grants; a deadline with reminders and escalations; and the optional workflow `ActivityKey`.
  - **Structure** is derived: one single-participant stage ⇒ **Single**; one multi-participant stage ⇒
    **Parallel**; several stages ⇒ **Sequential**.
  - **Policies** (`ApprovalPolicies`): `Single`, `AnyApprover`, `AllApprovers`, `Majority`, `Consensus`,
    `FirstResponse`, `Percentage(p)`, `Weighted(threshold)`.
- **Instance** — `ApprovalInstance`: status, outcome, current stage level, per-participant `ApprovalStep`s,
  context values, comments, deadline/reminder/escalation state, workflow link, audit history.

## Flow

```
workflow pauses on Activity("approve")
        │
        ▼
ApprovalEngine.StartForActivityAsync(def, ctx, workflowInstanceId, "approve")
        │  auto-rule match? → finish immediately
        │  else → activate stage 1, resolve participants          → ApprovalStarted / ApprovalAssigned
        ▼
ApproveAsync / RejectAsync (per participant)   → policy evaluated each vote
        │  stage approved & more stages → activate next stage (sequential)
        │  stage approved & last stage  → ApprovalCompleted(approved: true)  → workflow → "granted" branch
        │  stage rejected               → ApprovalCompleted(approved: false) → workflow → "denied" branch
        ▼
CancelAsync → cancels the workflow instance
RunDueAsync() (a scheduler tick): fires reminders · applies escalations · expires overdue approvals (→ denied)
```

## Usage

```csharp
services.AddApprovalEngine();          // also registers the workflow engine (idempotently)
// or: services.AddApprovalEngine(configuration);  // binds Workflow:Approvals

var def = ApprovalDefinition.Create("capex", "CAPEX Approval")
    .ForActivity("approve")
    .AddRule(new ApprovalRule("amount < 1000", ApprovalOutcome.Approved))   // auto-approve small amounts
    .AddSingle("manager", ApprovalAssignment.Role("line-manager"))          // stage 1
    .AddStage("finance", ApprovalPolicies.Majority,                          // stage 2 (sequential)
    [
        new ApprovalParticipant("f1", ApprovalAssignment.User("finance-a")),
        new ApprovalParticipant("f2", ApprovalAssignment.User("finance-b")),
        new ApprovalParticipant("f3", ApprovalAssignment.User("finance-c")),
    ])
    .WithDeadline(ApprovalDeadline.In(TimeSpan.FromHours(48)))
    .AddEscalation(new ApprovalEscalation(TimeSpan.FromHours(1), ApprovalAssignment.User("cfo")))
    .Build();

var approval = await approvalEngine.StartForActivityAsync(def, ctx, workflowInstanceId, "approve");
await approvalEngine.ApproveAsync(approval.Id, "manager", "u-alice");   // advances to the finance stage
```

## Notes

- **Reject and expiry route to the rejection branch** (`approved = false`); an approval routes to the approval
  branch (`approved = true`). The workflow's decision node branches on the `approved` outcome variable.
- **Escalation gives a new lease** — an escalated approval is reassigned and does not auto-expire.
- **Permissions** — the active approver implicitly holds view/approve/reject/comment; other rights come from
  the definition's grants (`ApprovalPermissionEvaluator`).
- **Persistence** defaults to in-memory (`IApprovalRepository`, `IApprovalStore`, `IApprovalHistoryRepository`);
  **events** go through `IApprovalEventSink` (the event-bus seam).
- **No secrets** — `sample.config.json` binds `Workflow:Approvals` only.

Out of scope for this commit (by the spec): the approval UI. Human task and forms surfaces are composed by the
orchestration layer, not by this engine.
