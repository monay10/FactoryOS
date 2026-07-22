# Connector Runtime (Commit 0021)

The layer that lets the platform **invoke** an external system: one request, one operation, one answer — with
retries, a circuit breaker, a rate limit, a cache, authorization, a credential, an audit line and a
measurement wrapped around it.

> **Where it lives.** Per Commit 0021's rules, **no new project was created and the existing connector
> framework was not changed.** The runtime lives inside `src/FactoryOS.Connectors/` under `Runtime/`, in the
> `FactoryOS.Connectors.Runtime.*` namespace, and builds on the framework beside it.

## What was already here, and what this adds

`src/FactoryOS.Connectors/Framework/` already had a great deal, and **none of it was rebuilt**:

| Already present | Where |
|---|---|
| `IConnector` / `IOutboundConnector`, `ConnectorManifest`, `SourceRecord` | `FactoryOS.Contracts.Connectors` |
| `ConnectorVersion`, `ConnectorCapability`, `ConnectorDescriptor`, `ConnectorContext` | `Framework/Runtime` |
| `ConnectorRegistry`, `ConnectorHost`, `ConnectorManager`, `ConnectorActivator`, `ConnectorCatalog` | `Framework/*` |
| `ConnectorConfiguration`, AES-GCM `IConnectorSecretProtector` | `Framework/Configuration`, `Framework/Security` |
| `IConnectorHealthService` (heartbeats, failure latch, recovery) | `Framework/Health` |
| Normalize / dedup / bind ingestion pipeline | `Pipeline/`, `Normalization/`, `Binding/` |

What was missing is the **invocation path**. A connector could stream records; it could not be *called*. There
was no named operation, no request or response, no resilience, no per-tenant activation of a connector, and
nothing that discovered a `connector.json` on disk. That is what this commit adds:

```
ConnectorDefinition  — what a connector kind is, and which operations it offers   (shared by every tenant)
ConnectorInstance    — one factory's activation of it: endpoint, credential, state  (owned by one tenant)
ConnectorRequest     — invoke this operation, on that instance, as this caller
ConnectorResponse    — what came back, what it cost, and why it failed if it did
```

Reusing rather than duplicating is the point. Version comparison, capability flags, secret encryption and
connector health all keep working exactly as before; a second copy of any of them would be a second thing to
get wrong, and the two would eventually disagree.

## Nothing already in the repository had to change

Every connector in `connectors/` — Logo, SAP, Netsis, Mikro, Oracle, SQL, REST, CSV, LDAP, Active Directory,
Entra ID, webhook, log — became invocable through the runtime **without a line being edited**. Two adapters do
it: `InboundConnectorOperationHandler` wraps an `IConnector` as the conventional `read` operation, and
`OutboundConnectorOperationHandler` wraps an `IOutboundConnector` as `deliver`. The connector contract stays a
tenant-scoped record stream, and the whole pipeline wraps around it from the outside.

## The pipeline

Every invocation travels through the same stages, outermost first. The order is fixed by the stage itself
rather than by registration order, because a pipeline whose behaviour depends on which file called `Add` first
changes meaning when someone tidies the composition root.

| # | Stage | Why it sits there |
|---|---|---|
| 1 | **Tracing** | Outermost, so every later stage shares one correlation, trace and request id |
| 2 | **Metrics** | Measures the whole call including the waits between retries — what the caller actually waited |
| 3 | **Monitoring** | Turns outcomes into the framework's existing health signals |
| 4 | **Audit** | Records the attempt *and the refusals* — the line most worth keeping |
| 5 | **Authorization** | Decides before anything is revealed |
| 6 | **Validation** | Rejects what no attempt could satisfy |
| 7 | **Authentication** | Resolves the credential the connector presents *outward* |
| 8 | **Caching** | A hit costs no permit and no circuit |
| 9 | **Retry** | Wraps the two below, so each attempt takes its own permit and consults the circuit |
| 10 | **Rate limit** | Inside retry, outside the circuit |
| 11 | **Circuit breaker** | The last guard before the external system is touched |
| 12 | **Transformation** | Shapes what came back, closest to the connector that produced it |

Three of those placements are worth stating plainly, because each is a decision somebody will one day want to
reverse:

- **Authorization runs before validation and before authentication.** Validating first would tell an
  unauthorized caller which parameters an operation takes; resolving a credential first would let one probe a
  secret store by watching how long a refusal took.
- **Retry wraps the rate limiter.** A retried attempt takes its own permit, so a burst of retries cannot push
  a factory past the quota its ERP vendor publishes.
- **The rate limiter wraps the circuit breaker.** Being throttled is our own doing, not the remote system
  failing, so it must never count toward opening a circuit against it.

## Retry, and the thing retries get wrong

Two conditions must **both** hold before an attempt is repeated, and they are separate on purpose:

- The **error** must be one a later attempt could survive. Retrying a malformed request spends three calls to
  be told the same thing three times.
- The **operation** must be idempotent. Retrying a non-idempotent write is how one purchase order becomes
  three, and no amount of backoff makes that safe. `ConnectorOperation.Idempotent` defaults to `false`, and
  `ConnectorRetryPolicy.None` is the default policy — a connector opts *into* being retried.

An open circuit is deliberately **not** retryable. The break outlives any backoff, so retrying in place would
only burn the attempts the caller needs once the circuit closes.

## Tenant isolation is structural

A connector instance's identity **is** `tenant|key`. Reaching another factory's ERP is not refused by a check
somebody could remove or misconfigure — there is no lookup that would find it. The store, the cache, the rate
limiter, the circuit breaker and the session manager are all keyed the same way, so one factory's exhausted
quota, open circuit or cached read cannot touch another's.

On top of that, the **pipeline** refuses a caller acting in one tenant against an instance belonging to
another — in the authorization stage, *before* the authorizer port is consulted — and the outbound adapter
refuses a message whose own tenant disagrees with the invocation's. Three independent gates, because this is
the one thing the Constitution says must never happen.

The tenant gate deliberately sits in the pipeline rather than in `IConnectorAuthorizer`. That port is
replaceable, and an adapter forwarding to a decision layer that only ever sees the *caller* cannot know which
tenant the *instance* belongs to — an easy omission that would silently open the one door that must not exist.
A port may decide permissions; it may not decide tenancy. An integration test wiring the platform's real
security engine found exactly this, and a unit test now pins it: an authorizer that allows everything still
cannot get a request across a tenant boundary.

## Secrets

A connector configuration stores **where a secret lives**, never the secret:

- `${secret:NAME}` — resolved at invocation time from the secret sources (a vault, the orchestrator, the
  environment).
- `enc:…` — decrypted with the framework's existing AES-GCM protector, reused rather than reimplemented.
- anything else — plaintext, so a development host with a local test database needs no vault.

A resolved secret is a `ConnectorSecret`, whose `ToString()` is masked. Formatting a credential — into a log,
an exception, an audit record, a diagnostic dump — cannot leak it; reading the real value takes the deliberate
call to `Reveal()`.

An unresolvable secret fails **at start-up and again at authentication**, not at the external system.
"Authentication failed" from an ERP after a thirty-second timeout and "the secret this instance names does not
exist" are the same fault with wildly different repair times.

## Health: five questions, not one

`ConnectorHealthEngine` asks **liveness, readiness, dependency, version and credential** separately and reports
the worst answer, because they fail separately and are fixed differently. An instance can be running and
unable to authenticate; it can authenticate and be pinned to a version that no longer offers the operation it
uses; it can be perfect and its ERP down. Collapsing that into one boolean produces "not working", which is
the reading that starts a shift on a connector nobody has diagnosed.

**Silence is not health.** An instance nothing has called yet reports `Unknown` for its dependency, not
`Healthy`.

## Security, audit and monitoring are ports

The runtime has no reference to the platform's security, audit or monitoring engines — it cannot see them, and
that is the design. It states what happened in its own vocabulary and a host adapter maps it:

| Port | Default | What a host substitutes |
|---|---|---|
| `IConnectorAuthorizer` | permissions the caller arrived holding | the platform's security engine |
| `IConnectorAuditSink` | bounded in-memory trail | the platform's audit engine |
| `IConnectorMetricSink` | bounded in-memory series | the platform's monitoring engine |
| `IConnectorRuntimeEventSink` | bounded in-memory history | the event bus, a SIEM forwarder |
| `IConnectorSecretSource` | environment variables | a vault |

Every one of them **fans out** to all registered subscribers rather than allowing a single consumer, so
observability, an event-bus forwarder and an exporter are three subscribers to one stream instead of three
layers of wrapping around one.

Permission strings use the platform's `resource.action` grammar, identical to the identity and security
layers', so a permission granted there is the same string checked here.

## Discovery

Each immediate subfolder of the discovery root containing a `connector.json` is one connector — the same
convention the plugin framework uses for `module.json`, deliberately, because a factory engineer should learn
the layout once. The runtime reads four **optional** additions to that manifest — `version`, `capabilities`,
`category` and `operations` — through the existing manifest reader, so a manifest written before this runtime
existed still loads and gets sensible defaults.

A folder whose manifest is invalid is **reported with its reason**, not skipped. A connector that silently
fails to appear is a support call; a connector that appears with its problem stated is a fix.

## Registering, and configuration only

```csharp
services.AddConnectorRuntime(configuration);   // binds Connectors and Connectors:Runtime
```

Onboarding a factory to an ERP is a `ConnectorInstance` — a tenant, a definition key, an endpoint, a
credential reference — and nothing else. There is no core code path that names a customer, and none that names
a vendor: `ConnectorCategory` is descriptive metadata the catalogue can be filtered by, never a branch.

## What is not configurable

There is no setting that disables authorization, none that lets a request reach another tenant's instance, and
none that makes a non-idempotent operation retryable in bulk. Those are invariants, not policy.

## Out of scope for this commit

No new connectors, no protocol implementations, no management screens, no distributed cache or broker-backed
event transport. This commit is the **runtime**: discovery, loading, activation, invocation, resilience,
security, health and the infrastructure underneath. The ports above are deliberately the shape those
integrations plug into.

## Tests

- **Unit** — `tests/FactoryOS.Tests/Connectors/ConnectorRuntimeTests.cs`: permissions, definitions and
  operations, instances, policies, retry, circuit breaker, rate limit, cache, sessions, secrets, discovery,
  manifests, compatibility, health and telemetry.
- **Integration** — `tests/FactoryOS.IntegrationTests/Connectors/ConnectorRuntimeIntegrationTests.cs`: the
  whole container, real connectors from `connectors/` invoked through the pipeline, credential resolution,
  persistence, scheduling, health, and the audit and monitoring ports.
