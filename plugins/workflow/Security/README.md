# Security Engine (Commit 0020)

The platform's **authorization decision layer**: who may do what, to which thing, from where, and when — with
an answer that always says *why*. It also owns sessions, token validity, and the violations and incidents that
follow from refusals.

> **Where it lives.** Per Commit 0020's rules, **no new project was created.** The engine lives inside the
> existing `FactoryOS.Plugins.Workflow` project under `Security/`, in the
> `FactoryOS.Plugins.Workflow.Security.*` namespace.

## What this is, and what it deliberately is not

`src/FactoryOS.Identity` already exists and already does a great deal: `Permission` (`resource.action` with
wildcards), `PermissionAuthorizer`, `ApplicationSession` with idle and absolute clocks, JWT access and refresh
tokens, `User` / `Role` / `Tenant`, `FactoryClaimTypes`, and the gateway's `RequirePermission` filters. **None
of that was rebuilt here**, because rebuilding it would have produced two authorization systems that
eventually disagree — which is worse than either alone.

This engine adds what Identity does not have, and stays compatible where it does:

| Concern | Where it lives |
|---|---|
| Authentication (passwords, credential checks) | `FactoryOS.Identity` — **not here** |
| JWT signing and signature verification | `FactoryOS.Identity` — **not here** |
| User and role storage | `FactoryOS.Identity` |
| Permission grammar (`resource.action`, wildcards) | **Identical in both, on purpose** |
| Session lifetime semantics (idle + absolute + revoke) | **Identical in both, on purpose** |
| ABAC, resource-, time- and network-based policy | Here |
| Explainable `SecurityDecision` | Here |
| Violations, risk and incidents | Here |
| Immediate token revocation | Here |

`SecurityClaim`'s well-known type names mirror `FactoryClaimTypes` exactly, so a principal assembled from a
FactoryOS access token becomes a `SecurityPrincipal` with no translation table.

## Where it sits

```
Workflow / Forms / HumanTask / Approval / Notification / SLA / Audit / Monitoring
                          ↓
                 Shared security vocabulary
                 (permission · principal · claim)
                          ↓
                    Security Engine
```

No engine holds an `ISecurityEngine`, `AuthorizationService` or `PermissionManager`. `AddSecurityEngine()`
depends on **no other engine at all** — a container can take security alone — and an integration test asserts
exactly that. Deleting the `Security/` folder leaves every engine working as before.

## The decision, and why it is in that order

```
1. Authenticated?   An anonymous principal holds nothing, whatever claims it presents.
2. Same tenant?     Checked structurally, before any policy runs. No rule can grant around it.
3. Any deny?        An explicit refusal ends it. Always.
4. Any grant?       From a matching rule, or from a permission the principal holds.
5. Otherwise, no.   Nothing is permitted by default.
```

Step 2 is an **invariant, not a policy**. The Constitution says there is no code path that reads or writes
across tenants, so cross-tenant is refused before the policy engine is consulted and cannot be configured
around. A single such attempt is recorded at `Critical` risk on its own — one refused read is a
misconfiguration; one attempt to read another factory's data is not, and averaging them into the same counter
would let the second hide behind the first.

**A denial always says why.** `SecurityDecision` carries the reason, the deciding policy and rule, the failed
constraint and the correlation. The question after a refusal is always "why?", and an authorization layer that
cannot answer it is one that gets worked around rather than fixed.

| Reason | What it tells the operator |
|---|---|
| `NoMatchingRule` | Nothing grants this to anyone — the policy is missing |
| `MissingPermission` | Rules exist; this principal is not covered by them |
| `ConstraintNotSatisfied` | You would be allowed, but *this* condition failed |
| `TenantMismatch` | Structural refusal; not fixable by a grant |
| `NotAuthenticated` / `TokenNotValid` / `SessionNotActive` | The credential, not the permission |

### One consequence, stated plainly

**A constraint narrows the rule it is attached to, not the permission.** A principal holding `audit.export`
outright is not stopped by a time window written on some *other* rule. To bind everybody regardless of what
they hold, write a **deny** — `PolicyLibrary.Prohibition` — and a deny always wins. This is the standard model
and both halves of it are covered by tests.

## The seven policy styles

`RoleBased` · `AttributeBased` · `ClaimBased` · `ResourceBased` · `TenantBased` · `TimeBased` · `IpBased`

They are not seven mechanisms; they are seven shapes of one — rules with constraints. Naming them separately
keeps a policy file readable without splitting the evaluator into seven code paths that would each need their
own proof that deny still beats allow.

A **tenant-based policy is configuration, not a branch in the core.** The engine never asks which tenant it is
serving; it asks the repository for the policies configured for the tenant in scope. Onboarding a factory adds
a policy, never a code path.

Constraints ship for time windows (including night shifts that cross midnight), CIDR network ranges, resource
attributes, claim-to-resource comparison (the workhorse of real ABAC — "approve orders *on your own site*"),
resource ownership and scope containment. Two rules they all follow:

- **Unknown is not permission.** A request with no network address does not satisfy an IP constraint —
  otherwise the constraint is bypassable by anything that omits the header it was read from.
- **Two unknowns are never a match.** A missing claim compared against a missing attribute would grant on the
  strength of knowing nothing.

## Sessions

Two clocks run at once. The **idle** window slides on every use, so somebody working is not thrown out
mid-task. The **absolute** lifetime never moves, so a session cannot be kept alive forever by a script that
touches it — which is exactly what a stolen session would do.

Reaching the concurrent-session limit **displaces the oldest session rather than refusing the new one**.
Refusing would be a denial-of-service you could aim at a colleague: fill their quota from a machine you already
control and they can no longer sign in. Displacing puts the cost on the attacker's own session, and the person
displaced finds out, because the session ends with a reason attached.

## Tokens

A **reference-token** model: the handle a caller presents is looked up, and lifetime, revocation, audience,
tenant and the bound session are read from the record rather than from the bearer's copy.

This is a deliberate choice over re-implementing signature verification. The platform already signs and
verifies JWTs where the signing key lives; a second crypto path here would be a second thing to get wrong. And
the one property a self-contained signed token cannot give you is the one that matters most in a factory —
**immediate revocation**. A stolen token that stays valid until it expires is not a token anybody can act on.
The two compose: the JWT's `jti` is the handle, the signature is verified where the key is, and "is it still
good?" is asked here.

Revoking a session revokes every token bound to it. A sign-out whose tokens still worked would be a sign-out
that signed nobody out.

## Violations, risk and incidents

A **violation is a fact**, recorded whether or not anybody will look at it. **Risk** is derived from what was
recorded, never typed in — a level somebody set by hand stops matching reality the day after. An **incident**
is raised once, **on the crossing** of the threshold, not on every violation past it: raising one per violation
afterwards would bury the moment the pattern actually appeared.

## Audit and monitoring integration

Both are **opt-in bridges that subscribe** — the runtime never calls the audit or monitoring engines, and
neither knows this engine exists:

```csharp
services.AddSecurityEngine();                  // core only; depends on no other engine
services.AddSecurityAuditIntegration();        // decisions, sessions and violations → the audit trail
services.AddSecurityMonitoringIntegration();   // the same stream → metrics
```

Refusal metrics are sliced by **reason** as well as permission, which is the slice that matters: a hundred
`MissingPermission` denials is somebody's role being wrong, and a single `TenantMismatch` is something else
entirely.

Two accommodations were needed to leave the other engines untouched, and both are honest rather than hidden:

1. `AuditAction` is a small set of stable verbs by design, so an administrative grant is recorded as
   `Changed` with `EventType = "PermissionGranted"` — the pattern Commit 0018 already established.
2. `MetricCategory` has no `Security` value, and adding one would have meant editing the monitoring engine.
   Security metrics are filed under `Infrastructure`. It interferes with nothing (health checks read named
   metric keys, not whole categories), and **a future commit allowed to touch monitoring should give security
   a category of its own.**

## Correlation

`CorrelationId`, `TraceId` and `RequestId` are carried verbatim from the request onto the decision, the events,
the violation, the audit record and the metric. A record of a denial that cannot be joined to the request it
refused is one nobody can act on.

## What is not configurable

There is no setting that turns authorization off, none that makes an allow outrank a deny, and none that
permits reaching across tenants. Those are the three settings every authorization system eventually grows and
then gets breached through, so they are not settings.

## Out of scope for this commit

No user, role or identity-provider management screens; no LDAP/Active Directory connectors; no OAuth or
OpenID Connect provider integration; no security dashboards; no SIEM integration. This commit is the
**runtime**: authorization, policy evaluation, session management and the infrastructure underneath. The
event seam is deliberately the shape a SIEM forwarder will plug into.

## Tests

- **Unit** — `tests/FactoryOS.Tests/Workflow/SecurityEngineCoreTests.cs`: permissions, roles, claims, policies,
  rules, constraints, sessions, tokens, authorization, violations, risk and correlation.
- **Integration** — `tests/FactoryOS.IntegrationTests/Workflow/SecurityEngineIntegrationTests.cs`: guarding
  workflow, forms, human task, approval and connector operations through a real container, persistence,
  session management, and the audit and monitoring bridges.
