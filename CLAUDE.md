# FactoryOS — Engineering Constitution & Architecture

> **FactoryOS is an AI-Native Industrial Operating System.**
> It does for factories what Windows did for computers: a stable **Core** (the OS),
> **Modules** (the apps), **Connectors** (the drivers), **AI Agents** (digital workers),
> and a **Store** (the marketplace). Every customer runs the same core; only the set of
> activated plugins differs.

This file is the single source of truth for how FactoryOS is built. Every Claude Code
session and every human contributor MUST obey it. If a task appears to require breaking a
Law below, stop and raise it — do not work around it.

---

## 1. The Constitution (non-negotiable Laws)

> **The authoritative, immutable Constitution is [docs/CONSTITUTION.md](docs/CONSTITUTION.md)
> (26 rules, ratified 2026-07-19).** It always wins over any other document. The Laws below
> are the architectural expression of those rules — read them together, never in conflict.

1. **Modular core.** The core contains **no customer-specific code, ever.** There is never
   an `if (tenant == "ABC")`. Behavior varies by configuration and by which plugins are
   active — never by branching on a customer.
2. **Everything is a plugin.** All features ship as plugins under `plugins/`. Connectors,
   AI agents, dashboards, reports and widgets are plugins too. The core only knows the
   *contract*, never a plugin's name.
3. **Connectors are the only door to the outside.** ERP, PLC and third-party systems are
   reached exclusively through the Connector layer. No business module ever talks to Logo,
   SAP, an OPC-UA endpoint or a raw SQL source directly.
4. **Event-driven only.** Modules never call each other. They **publish events** and
   **subscribe to events**. No direct module-to-module references, no shared in-process calls.
5. **Independently installable.** Every plugin can be installed and removed on its own
   without touching the core or other plugins.
6. **Multi-tenant by construction.** Every factory is an isolated tenant. Data, config,
   branding and module set are per-tenant.
7. **Layered separation.** API, service/application, and UI layers are always separated.
8. **Every feature ships with tests, docs and a sample config.** No feature is "done"
   without automated tests, documentation, and an example configuration file.
9. **Docker-first.** The whole system runs under Docker. Development and production
   environments are separated.
10. **New customer = configuration only.** Onboarding a factory is done purely by
    configuration and module selection. The core code is never modified to add a customer.

### Derived invariants (how the Laws are enforced technically)

- **The Standard Model is the only shared language.** No business module or AI agent ever
  speaks an ERP/PLC dialect. Connectors normalize everything into canonical entities
  (`InventoryItem`, `Asset`, `MeterReading`, `WorkOrder`, …). `LogoStock`, `SAP.Material`,
  `Netsis.ItemCard` all become `InventoryItem`. Mapping is **data (a manifest), not code.**
- **Contracts over names.** The core discovers plugins via manifests (`module.json`,
  `agent.json`, `connector.json`) and wires them through interfaces. Deleting a plugin
  folder removes the feature with zero core diff.
- **Idempotent consumers.** Event delivery is **at-least-once**; every consumer deduplicates
  by event `id`. Ordering is guaranteed **per aggregate** (e.g. per machine), not globally.
- **Tenant is always in scope.** Every event, query and job carries a `tenant`. There is no
  code path that can read or write across tenants.

---

## 2. Locked technology stack

| Concern | Choice |
|---|---|
| Backend | **ASP.NET Core 10 (LTS) / C#**, Clean Architecture, DDD, CQRS (where it earns its keep), MediatR |
| Persistence | **PostgreSQL** + **EF Core**, **schema-per-tenant** isolation |
| Messaging / Event Bus | **MassTransit** over **RabbitMQ** (broker is abstracted & swappable) |
| Cache / read-models | **Redis** |
| Object storage | **MinIO** (S3-compatible) |
| Frontend | **React + TypeScript + Tailwind CSS**, component-based, **PWA** |
| AI | **LLM Gateway** → OpenAI-compatible API + **Ollama** (local); Embeddings, **RAG**, **MCP**, Agent Framework. AI is called over HTTP — never in-process. |
| IoT | **MQTT, OPC-UA, Modbus TCP/RTU, Siemens S7**, via an **Edge Gateway** |
| Runtime | **Docker** everywhere; **Kubernetes** optional for scale |

**Plugin isolation strategy:** first-party plugins run as a **modular monolith**
(assemblies discovered by manifest, loaded via `AssemblyLoadContext`). Third-party **Store**
plugins run **out-of-process / sandboxed** (separate container, access limited to the Event
Bus and permitted APIs). Design first-party contracts today so the out-of-process transition
(Phase 5) is seamless.

---

## 3. Layered architecture

```
Presentation   Web · Mobile (PWA) · Wall Dashboard · Digital Twin
AI             Company Brain · AI Agents · RAG · LLM Gateway · Prompt Engine
Business       Energy · Maintenance · Quality · Production · OEE · Warehouse ·
               Procurement · HR · Carbon · Safety · Workflow      (all plugins)
Platform       Identity · Tenant · Notification · Rule Engine · Scheduler ·
               Event Bus · File Storage
Integration    Logo · SAP · Netsis · Mikro · Oracle · SQL · REST · MQTT · OPC-UA · Modbus · PLC
Infrastructure PostgreSQL · Redis · MinIO · RabbitMQ · Docker · Kubernetes
```

Data flow spine — **everyone speaks on the Event Bus, in the Standard Model**:

```
PLC / Sensors ──(OPC-UA/Modbus/MQTT/S7)──► Edge Gateway ─┐
ERP (Logo/SAP/Netsis/…) ──(Connector + mapping)──────────┤─► Standard Model
                                                          ▼
              ┌──────────────── EVENT BUS (tenant-namespaced) ───────────────┐
              ▼            ▼             ▼            ▼            ▼           ▼
           Modules    Rule Engine    AI Agents   Company Brain  History   Dashboard
```

---

## 4. Repository layout

```
FactoryOS/
  src/
    Core/
      FactoryOS.Domain/          # Standard Model entities, value objects, domain events
      FactoryOS.Application/     # CQRS handlers (MediatR), plugin/agent/connector contracts
      FactoryOS.Infrastructure/  # EF Core, Postgres, Redis, MinIO, MassTransit, LLM Gateway
      FactoryOS.Api/             # ASP.NET Core host, plugin loader bootstrap, API gateway
    Platform/                    # Identity, Tenant, Notification, RuleEngine, Scheduler, EventBus, FileStorage
    SharedKernel/                # cross-cutting primitives, event envelope, result types
  plugins/                       # business modules — each self-contained, manifest-driven
    energy/  maintenance/  oee/  quality/  production/  warehouse/ ...
  connectors/                    # the "drivers" — each a plugin
    logo/  sap/  netsis/  opcua/  mqtt/  modbus/  rest/  sql/  csv/ ...
  agents/                        # AI agents — same runtime, differ only by manifest+prompt
  edge/                          # Edge Gateway (IoT ingestion, protocol adapters)
  web/                           # React + TS + Tailwind PWA
  deploy/                        # docker-compose (dev), k8s manifests (prod)
  docs/                          # architecture, contracts, roadmap
```

Every plugin is self-contained:

```
plugins/<name>/
  module.json     # manifest: key, version, requires, provides, consumes, emits
  api/            # endpoints (mounted by core at /m/<name>/*)
  application/    # CQRS handlers, services
  domain/         # module-local domain (reads Standard Model, never ERP dialects)
  ui/             # React screens, lazy-loaded by the dashboard
  agents/         # module-scoped agents (optional)
  rules/          # rule-engine definitions
  events/         # published/subscribed event declarations
  migrations/     # tenant-scoped schema
  tests/          # required
  sample.config.json  # required
```

---

## 5. Core contracts

- **Event envelope** — every message on the bus: `{ id, type, version, tenant, source,
  occurredAt, correlationId, causationId, payload, meta }`. See
  [event-envelope.schema.json](docs/contracts/event-envelope.schema.json).
- **Module manifest** — [module.schema.json](docs/contracts/module.schema.json).
- **Tenant config** — [tenant.schema.json](docs/contracts/tenant.schema.json). A new customer
  is one `tenant.json`: modules, branding, ERP, PLC, AI model, language, units, timezone.
- **History = Event Store.** All events are appended to a per-tenant event store, giving
  audit, replay and read-model rebuild for free.

---

## 6. How we build: sprint process

Development proceeds in ~40–50 sprints (see [docs/ROADMAP.md](docs/ROADMAP.md)). Order:
**Phase 0 Core → Integration → AI Platform → Business Modules → Experience → Marketplace.**
Never build a business module before the core contracts it depends on exist.

**Definition of Done (every task):**
- [ ] Conforms to all Laws in §1 (no core-side customer code; event-driven; connector-gated).
- [ ] Automated tests pass (unit + integration where I/O is involved).
- [ ] Documentation updated.
- [ ] Sample configuration file provided.
- [ ] Runs under `docker-compose` in the dev profile.

**When in doubt:** prefer a new plugin over a core change. If a change *seems* to require
editing the core to satisfy one customer, it is wrong by construction — surface it.
