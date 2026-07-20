# FactoryOS Roadmap

Built in ~40–50 sprints. The order is deliberate: **the core and its contracts come first**;
business modules are only built on top of a core that already provides what they consume.
Every sprint obeys the [Constitution](../CLAUDE.md) and its Definition of Done.

---

## Phase 0 — Core Platform (the OS kernel)

The stable core that never changes for a customer. Nothing else starts until these contracts exist.

| Sprint | Deliverable |
|---|---|
| 0 | **Solution skeleton** — Clean Architecture layout, Docker dev profile, PostgreSQL, CI, logging/observability |
| 1 | **Identity & Auth** — users, roles, permissions, JWT, per-tenant auth boundary |
| 2 | **Tenant Management** — `tenant.json` contract, schema-per-tenant provisioning, config service |
| 3 | **Event Bus** — MassTransit/RabbitMQ, event envelope, at-least-once + idempotency, per-aggregate ordering |
| 4 | **Event Store & History** — append-only per-tenant log, replay, audit, read-model rebuild |
| 5 | **Plugin Framework** — `module.json`, manifest discovery, `AssemblyLoadContext` loading, lifecycle (install/enable/disable) |
| 6 | **Module Loader + API Gateway** — dynamic route mounting (`/m/<name>/*`), UI lazy-load registry |
| 7 | **Connector Framework** — connector contract, **Standard Model**, mapping-as-data, dedup/normalize pipeline |
| 8 | **Rule Engine + Scheduler + Notification** — platform services shared by all modules |
| 9 | **File Storage** — MinIO integration, per-tenant buckets |

**Exit criterion:** drop a trivial plugin folder in, and the running core discovers, mounts,
migrates and serves it — with **zero core diff**.

---

## Phase 1 — Integration (the drivers)

Real connectors on the framework. The Connector Marketplace begins here.

| Sprint | Deliverable |
|---|---|
| 10 | **SQL / REST / CSV** connectors (generic sources) |
| 11 | **Logo** connector (+ mapping to Standard Model) |
| 12 | **Netsis / Mikro** connectors |
| 13 | **SAP / Oracle** connectors |
| 14 | **IoT Hub** — device registry, tags, telemetry model |
| 15 | **Edge Gateway** — MQTT, OPC-UA |
| 16 | **Edge Gateway** — Modbus TCP/RTU, Siemens S7 |

---

## Phase 2 — AI Platform (the digital workers)

| Sprint | Deliverable |
|---|---|
| 17 | **LLM Gateway** — OpenAI-compatible + Ollama, model routing per tenant |
| 18 | **Prompt Engine + Embeddings** |
| 19 | **RAG + Knowledge Base** |
| 20 | **Company Brain** — organizational memory/context |
| 21 | **Agent Framework** — `agent.json`, BaseAgent (perceive→reason→act), MCP tools |
| 22 | **Agent memory + tool registry** — agents act via module APIs and emit events |

---

## Phase 3 — Business Modules (the apps) — one per sprint

Each is a plugin: `api / application / domain / ui / rules / events / migrations / tests / sample.config`.
Each ships with at least one agent and one dashboard where it makes sense.

| Sprint | Module |
|---|---|
| 23 | Energy |
| 24 | Maintenance (Predictive) |
| 25 | OEE |
| 26 | Quality |
| 27 | Production |
| 28 | Warehouse |
| 29 | Procurement |
| 30 | HR |
| 31 | Carbon |
| 32 | Safety |
| 33 | Workflow (BPMN, approvals, forms, tasks) |
| 34 | Fleet |
| 35 | Vision AI |
| 36 | Process Factory (no-code module/process builder) |

---

## Phase 4 — Experience

| Sprint | Deliverable |
|---|---|
| 37 | Dashboard framework + Dashboard Marketplace (role-based screens: CEO, Plant Manager, Maintenance…) |
| 38 | Wall Dashboard |
| 39 | Digital Twin |
| 40 | Mobile PWA |
| 41 | Reporting / Executive Dashboard |

---

## Phase 5 — Marketplace & Store (the ecosystem — the long-term moat)

| Sprint | Deliverable |
|---|---|
| 42 | **Out-of-process plugin runtime** — sandboxed 3rd-party modules |
| 43 | **FactoryOS Store** — publish/install flow, versioning, permissions |
| 44 | **Third-party SDK** — build a Module / Connector / Dashboard / Widget / Report / AI agent |
| 45 | Connector Marketplace (incl. new ERPs like IFS with zero core change) |
| 46 | AI Agent Marketplace |
| 47 | Dashboard / Widget Marketplace |
| 48 | IoT device catalog (ESP32, Arduino, Siemens, Omron, Delta, Beckhoff, Schneider, Wago, Advantech, ICP DAS) |
| 49 | API Marketplace (REST, GraphQL, WebSocket, MQTT, OPC-UA, gRPC surfaces) |
| 50 | Billing, licensing & metering per module/agent/tenant |

---

*This roadmap is a living document. Sprint numbers are sequencing, not commitments; scope
moves between sprints as long as the phase ordering (core → integration → AI → modules →
experience → marketplace) holds.*
