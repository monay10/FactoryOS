# Audit Engine (Commit 0018)

The platform's **immutable, hash-chained audit trail**. Every notable thing the workflow, forms, human task,
approval, notification and SLA engines do becomes a sealed record in a per-tenant chain that can be searched,
correlated, archived, retained, exported — and, crucially, **verified**: if a record is edited, removed or
reordered, the chain says so.

> **Where it lives.** Per Commit 0018's rules, **no new project was created.** The engine lives inside the
> existing `FactoryOS.Plugins.Workflow` project under `Audit/`, in the `FactoryOS.Plugins.Workflow.Audit.*`
> namespace.

## Audit is the bottom of the stack

```
Engine → Bridge → Application → SLA → Notification → Audit → Metrics
```

Audit consumes what every engine above it publishes and writes **nothing** back. No engine references the audit
namespace, and none was modified to be audited.

### How it reaches six event streams at once

The SLA engine's event seam already fans out, so audit simply registers alongside the existing consumers. The
other five seams (`IWorkflowEventSink`, `IFormEventSink`, `IHumanTaskEventSink`, `IApprovalEventSink`,
`INotificationEventSink`) allow only **one** consumer — and notifications already held four of them. Rather
than change those engines, `AddAuditEngine()` **wraps whatever is registered in a composite and appends
itself**:

```
IWorkflowEventSink ─► CompositeWorkflowEventSink ─┬─► WorkflowNotificationSubscriber   (unchanged, still fed)
                                                  └─► WorkflowAuditSubscriber
```

Every prior consumer keeps receiving exactly what it did before, unaware. This is the fan-out the platform
event bus will eventually provide for everyone; until then it is introduced here, where it is first needed.

## Immutability, and how it is enforced

Two independent mechanisms, because either alone would be theatre:

1. **The type cannot be changed.** `AuditRecord` is sealed, every property is get-only, and there is no mutator
   anywhere on it. The store exposes no update operation at all — records go in, are read, and can only leave
   by being archived or expiring.
2. **The content is chained.** Each record carries its per-tenant `Sequence`, the `PreviousHash` of the record
   before it, and a SHA-256 `Hash` over its own canonical content *including* that previous hash.

Records are created in exactly two ways: `Seal` computes the hash for a new record; `Rehydrate` reconstructs one
from storage **with the hash it was stored with**. Storage never recomputes a hash it reads — that is precisely
what lets `RecomputeHash()` expose a tampered row instead of silently repairing it.

`Verify(tenant)` reports the first break and why:

| What was done to the trail | What verification says |
|---|---|
| A field was edited | *content does not match its hash* |
| A record was swapped for a forgery | *does not link to its predecessor* |
| Records were reordered | *out of sequence* |

Chains are **per tenant**: each tenant's sequence starts at 1 and links only to its own records, so nothing
crosses tenants even in the audit trail. Archiving legitimately leaves gaps, so linkage is asserted only between
records whose sequence numbers are consecutive; an archived stretch verifies on its own terms.

## Sources

| Source | How it arrives |
|---|---|
| Workflow, Forms, Human Task, Approval, Notification | subscriber wrapped into the engine's seam by a composite |
| SLA | subscriber added to the seam's existing fan-out |
| Authentication, Authorization, Configuration, Connector, Plugin | `AuditEntries.*` factories the platform calls directly |

## Usage

```csharp
services.AddAuditEngine();          // composes the engines it audits and wires the composites

audit.RegisterArchive(new AuditArchivePolicy(TimeSpan.FromDays(90)));
audit.RegisterRetention(new AuditRetentionPolicy(TimeSpan.FromDays(365)));
audit.RegisterRetention(new AuditRetentionPolicy(               // security records are kept, never deleted
    TimeSpan.FromDays(2555), AuditRetentionAction.Archive, AuditCategory.Authentication));

audit.Record(AuditEntries.SignIn("acme", "u-alice", succeeded: true));

var trail = audit.Search(new AuditQuery { Tenant = "acme", CorrelationId = orderId, IncludeArchived = true });
var verdict = audit.Verify("acme");                              // tamper detection
var csv = audit.Export(new AuditQuery { Tenant = "acme" }, AuditExportFormat.Csv, "u-auditor");

audit.ArchiveDue();                 // a scheduler tick
audit.RunRetention("acme");
```

## Notes

- **Correlation is preserved verbatim.** `CorrelationId`, `TraceId`, `SessionId`, `RequestId` and `CausationId`
  are carried through untouched and are part of the hashed content, so they cannot be swapped without breaking
  the chain. Engine-sourced records correlate on the aggregate they concern, which is what makes one operation's
  trail pull together across six engines.
- **Sessions are a projection**, derived from the records sharing a session id — never stored, so they cannot
  drift from the trail they summarise.
- **Exporting is itself audited**, and exports carry each record's hashes so the recipient can verify the chain
  rather than having to trust the exporting system.
- **Archiving is a storage decision, never a deletion.** Sequence numbers and hashes travel with a record into
  the archive and back out again unchanged.
- **Filtering happens at the door**, not at read time: a record that was never admitted cannot later be deleted
  to hide something.
- **Persistence** defaults to in-memory (`IAuditRepository` for policies, `IAuditStore` for the hot trail,
  `IAuditArchiveRepository` for the archive); **events** go through `IAuditEventSink`, which fans out.
- **No secrets** — `sample.config.json` binds `Workflow:Audit` only.

Out of scope for this commit (by the spec): the audit browser UI, and a durable store — the in-memory
implementations are the seam a Postgres-backed one plugs into.
