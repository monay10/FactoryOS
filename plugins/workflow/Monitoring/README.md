# Monitoring & Metrics Engine (Commit 0019)

The platform's **observability layer**: what every engine did, how long it took, how much of it went wrong, and
whether the whole thing is healthy right now. Measurements are collected from the events the engines already
publish, aggregated over windows, judged against thresholds, escalated into alerts, and retained or rolled up —
without a single engine knowing it is being watched.

> **Where it lives.** Per Commit 0019's rules, **no new project was created.** The engine lives inside the
> existing `FactoryOS.Plugins.Workflow` project under `Monitoring/`, in the
> `FactoryOS.Plugins.Workflow.Monitoring.*` namespace.

## Monitoring is the outermost layer

```
Engine → Bridge → Application → SLA → Notification → Audit → Metrics
```

Monitoring consumes what every engine above it publishes and writes **nothing** back. No engine references the
monitoring namespace, no engine holds an `IMonitoringEngine`, `MetricService` or `HealthService`, and none was
modified to be measured. Delete the `Monitoring/` folder and every engine keeps working exactly as before.

```
Workflow / Forms / HumanTask / Approval / Notification / SLA / Audit
                          ↓
                  Event & Bridge Layer
                          ↓
                   Monitoring Engine
```

### How it reaches seven event streams at once

The SLA and audit seams already fan out, so their bridges simply register alongside the existing consumers. The
other five (`IWorkflowEventSink`, `IFormEventSink`, `IHumanTaskEventSink`, `IApprovalEventSink`,
`INotificationEventSink`) allow only **one** consumer — and notifications and audit already hold them. Rather
than change those engines, `AddMonitoringEngine()` **wraps whatever is registered and appends itself**:

```
IWorkflowEventSink ─► WorkflowMetricsBridge ─┬─► CompositeWorkflowEventSink ─┬─► Notifications  (still fed)
                                             │                              └─► Audit          (still fed)
                                             └─► measure
```

Forwarding happens **first**, so every prior consumer sees the event on exactly the path it always did. This is
the third layer to need the same fan-out; the standing plan is still to replace all of it with the
Constitution's MassTransit/RabbitMQ event bus, at which point every wrapper here becomes one subscription.

**Being observed can never fail the observed.** If a bridge cannot record what an event described, the failure
is contained and counted in `MonitoringMetrics.BridgeFaults` rather than thrown back into a workflow
transition. Containment without a counter would just be silence, which is why both halves are there.

## The metric model

| Type | What it is |
|---|---|
| `MetricDefinition` | What a metric *means* — key, category, kind, unit. Identical for every tenant. |
| `MetricDimension` / `MetricLabel` | The labels that separate one series from another. |
| `MetricInstance` | A series identity: **tenant** + metric + dimension. The tenant is part of the key. |
| `MetricValue` | One measurement: number, time, correlation, sampling weight. |
| `MetricSeries` | The stored history of an instance. |
| `MetricSnapshot` | What a series looked like over a window, collapsed to one number. |

`MetricKind` is not decoration — it decides what sampling may do (below) and what aggregation a reader gets by
default (a counter sums, a gauge takes the last value, a duration averages). A default that contradicted the
kind would be a bug waiting for a dashboard to reveal it.

Series identity is the dimension's **canonical, ordinally sorted rendering**, so labels supplied in a different
order still land in the same series rather than silently splitting one series into two.

### Windows are `(from, to]`

Exclusive start, **inclusive end**. Almost every window a reader asks for ends at "now", and with an exclusive
end a measurement taken this instant would be invisible until the clock moved past it — a dashboard forever one
tick behind what it watches. Making the start exclusive in exchange keeps consecutive windows a clean partition
with nothing counted twice.

### Sampling costs resolution, never correctness

The sampler is **deterministic** (one in every N per series, not dice) so the same traffic always produces the
same series and a test can assert on it. It is also **kind-aware**:

- dropping four of five **gauge** readings loses nothing an average cares about — the kept value weighs 1;
- dropping four of five **counter** increments would understate a total by eighty percent — so the kept value
  carries the weight of the run it stands for, and `Sum` over a series sampled at 1-in-10 still reports the
  true total.

`SampleRate = 0` is rejected: "measure this, but never record it" is never what anybody means.

## Thresholds, alerts and the two silences

A `MetricThreshold` judges an aggregated snapshot, never a single measurement — one slow request is not an
incident, and a threshold that fired on one would train everybody to ignore it. Each series of a metric is
judged **separately** unless the threshold names a dimension, so one collapsed email channel trips a
delivery-failure threshold even while the other channels are fine.

A `MetricAlertRule` adds what a raw threshold lacks: how bad (`TriggersAt`) and for how long (`For`). Two
decisions here separate an alert somebody acts on from one everybody mutes:

1. **The breach must survive `For` unbroken** before the alert opens, so a spike pages nobody.
2. **A metric that goes silent does not resolve an open alert.** A series that stops producing has not
   recovered; more often it stopped because whatever produced it died. An alerting layer that treated silence as
   recovery would close the alert at exactly the moment it matters most.

Alerts are **derived, not stored**: they are what the retained series say right now. After a restart the
evaluator re-derives what is still true, and consumers deduplicate by alert key — which the platform's
at-least-once delivery already requires of them.

## Health: twelve components, judged on outcomes

`Workflow Engine`, `Forms Engine`, `Human Task Engine`, `Approval Engine`, `Notification Engine`, `SLA Engine`,
`Audit Engine`, `Connectors`, `Plugins`, `Database`, `Storage`, `Configuration`.

Every probe reads **what a component actually did** rather than asking it how it feels. A probe that called into
the workflow runtime would put an arrow from monitoring back to the engine it observes — the exact dependency
this layer exists to avoid — and it would answer "healthy" for a runtime that is up but failing every instance
it starts.

- A component with **no signal** is `Unknown`, never `Healthy`. Silence is not evidence that anything works, and
  a report that rounds it up says "fine" during an outage.
- A **critical** component failing takes the whole report down; a non-critical one degrades it. Marking
  everything critical would be the same as marking nothing.
- A probe that **throws or hangs** degrades its own component and nothing else. The layer that tells you whether
  the platform is up must not be able to take it down.

## The thirteen collections

`Workflow · Forms · HumanTask · Approval · Notification · SLA · Audit · Connector · Plugin · Api ·
Infrastructure · Runtime · Performance`

The catalogue is the same for every tenant: `tasks.completed` counts completed human tasks whether the factory
is in Dudullu or anywhere else. That is Law 1 as it applies to measurement — there is no branch here on who is
being measured. The first seven are fed by bridges; the rest are fed by whoever performs the work (a connector
call, an API request, a configuration reload), which is the same consumer relationship expressed as a call
rather than a subscription.

Two distinctions the catalogue preserves deliberately:

- **`sla.breached` ≠ `sla.timedout`** — a missed deadline keeps running, a hard timeout ends the SLA. Collapsing
  them would erase the distinction the SLA engine was built to make.
- **`notifications.failed` ≠ `notifications.deadlettered`** — a failed attempt may retry; a dead letter is the
  end of the road. An alert needs to tell "retrying" from "given up".

## Correlation

`CorrelationId`, `TraceId` and `RequestId` travel with every measurement, verbatim — monitoring never
regenerates them. `ByCorrelation()` and `ByTrace()` turn *"this alert fired"* into *"here is the request that
caused it"*, and a snapshot carries the correlation of its most recent measurement so a `ThresholdExceeded` can
name the operation that tripped it.

## Retention

The most specific matching policy wins (metric → category → global), and there is **always** an answer: a metric
with no policy falls back to the engine default, because a store with no ceiling is a memory leak with a
dashboard attached. `RollUp` collapses old buckets instead of dropping them; a rolled-up sum weighs 1 (it has
already absorbed its bucket) while a rolled-up average carries the count behind it, so a later average over the
roll-up still lands where it should.

## Events

`MetricCollected` · `MetricAggregated` · `ThresholdExceeded` · `HealthCheckCompleted` · `HealthStatusChanged` ·
`MetricRetentionExpired` · `AlertTriggered` · `AlertResolved`

`MetricCollected` is **off by default** (`PublishCollectionEvents`). Monitoring sees every event every engine
raises; re-publishing each one would make the observability layer by far the loudest producer on the bus. Turn
it on when something downstream genuinely needs the raw stream. `HealthStatusChanged` fires only on a
transition — everything wants to know about a change, almost nothing wants to know that a healthy component is
still healthy.

## Registration

```csharp
services.AddMonitoringEngine();                 // brings audit, and through it every engine below
services.AddMonitoringEngine(configuration);    // binds Workflow:Monitoring
```

The container registers the whole catalogue and all twelve health checks as the engine is built, so a resolved
engine always has something to measure against — never an empty registry.

`MonitoringPermission` (`ViewMetrics`, `ViewHealth`, `ManageThresholds`) grants nothing by default: metrics say
how much work a factory did and where it went wrong, which is not public by omission.

## Out of scope for this commit

No Grafana or Kibana dashboards, no Prometheus or OpenTelemetry exporter, no live monitoring, reporting or alarm
management screens. This commit is the **runtime**: metrics, health, aggregation, alert evaluation and the
infrastructure underneath them. The event seam is deliberately the shape an exporter will plug into.

## Tests

- **Unit** — `tests/FactoryOS.Tests/Workflow/MonitoringEngineCoreTests.cs`: collection, sampling, aggregation,
  retention and roll-up, thresholds, alerts, health, search, correlation, permissions and the engine's own
  counters.
- **Integration** — `tests/FactoryOS.IntegrationTests/Workflow/MonitoringEngineIntegrationTests.cs`: the engine
  composed through a real container, measuring all seven engines, and proving the consumers that were on those
  seams before are still fed.
