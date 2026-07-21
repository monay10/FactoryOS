# Forms Engine (Commit 0013)

A dynamic **form runtime** for FactoryOS: it describes forms as data, renders them, validates input,
saves drafts, and submits them — and, when a form is bound to a workflow activity, completes that
activity so the workflow advances.

> **Where it lives.** Per Commit 0013's rules, **no new project was created.** The forms engine lives
> inside the existing `FactoryOS.Plugins.Workflow` project under `Forms/`, in the
> `FactoryOS.Plugins.Forms.Engine.*` namespace — next to the workflow process runtime
> (`FactoryOS.Plugins.Workflow.Engine.*`). The reactive workflow rule engine and the workflow process
> runtime are **untouched**; the only link is a one-way bridge (`IFormWorkflowBridge`) that calls the
> existing `WorkflowEngine.CompleteActivityAsync` on a valid submission.

## Model

- **Definition** — `FormDefinition` (built by `FormDefinition.Create(...).AddSection(...).Build()`) holds
  a section → group → field tree, a `FormLayout` (stack or N-column grid), permissions, an assignment,
  and an optional `ActivityKey` naming the workflow activity it satisfies.
- **Fields** — `FieldDefinition` carries a `FieldType`, static `FieldValidation` (required, pattern,
  min/max, length), choice `FieldOption`s, and conditional `FieldRule`s. Rules use the shared workflow
  expression language (e.g. `amount > 1000`) to drive **visibility, enablement, required-ness and
  calculated values**.
- **Instance** — `FormInstance` is one filling: values, audit `FormHistory`, resolved assignee, state
  (`Open → Draft → Submitted → Approved/Rejected/Cancelled`), and, when workflow-bound, the linked
  workflow instance and node.

## Flow

```
workflow pauses on Activity("review")
        │
        ▼
FormEngine.OpenForActivityAsync(def, ctx, workflowInstanceId, "review")   → FormOpened
        │  SaveDraftAsync(...)                                            → FormSaved   (no validation)
        ▼
FormEngine.SubmitAsync(...)
        │  invalid  → blocked, instance stays editable, workflow still paused
        │  valid    → FormSubmitted, snapshot captured, bridge completes "review"
        ▼
workflow advances to completion
```

## Usage

```csharp
services.AddFormsEngine();            // also registers the workflow engine (idempotently)
// or: services.AddFormsEngine(configuration);  // binds Forms:Engine

var form = FormDefinition.Create("review-form", "Review")
    .ForActivity("review")
    .AddSection(new FormSection("s", "Decision",
    [
        new FormGroup("g", null,
        [
            new FormField(new FieldDefinition("note", "Note", FieldType.Text)
            {
                Validation = new FieldValidation { Required = true, MinLength = 3 },
            }),
        ]),
    ]))
    .Build();

var instance = await formEngine.OpenForActivityAsync(form, FormContext.Default, workflowInstanceId, "review");
await formEngine.SaveDraftAsync(instance.Id, new Dictionary<string, object?> { ["note"] = "wip" });
var result = await formEngine.SubmitAsync(instance.Id, new Dictionary<string, object?> { ["note"] = "looks good" });
// result.IsAccepted == true → the workflow's "review" activity has completed
```

## Notes

- **Validation blocks submission.** `SubmitAsync` validates first; on failure it returns the
  `ValidationResult`, changes no state, and never touches the workflow. `ValidationSummary.From(...)`
  turns the result into a display model.
- **Rendering** (`FormRenderer` / `FormEngine.Render`) drops hidden fields and lays visible ones onto
  the grid — a pure read model.
- **Persistence** defaults to in-memory (`IFormRepository`, `IFormStore`, `IFormVersionRepository`,
  `IFormSubmissionRepository`); swap the registrations for a durable store later.
- **Events** are published through `IFormEventSink` (the event-bus seam).
- **No secrets** — forms carry no credentials; `sample.config.json` binds `Forms:Engine` only.

Out of scope for this commit (by the spec): BPMN designer, human-task UI, approval UI, notifications.
