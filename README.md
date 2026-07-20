# FactoryOS

**AI-Native Industrial Operating System.**

FactoryOS unifies every digital system in a factory — ERP, PLC/IoT, workflow, analytics and
AI — on a single platform. It does for factories what Windows did for computers:

| Windows | FactoryOS |
|---|---|
| Operating system | **Core** |
| Applications | **Modules** (`plugins/`) |
| Drivers | **Connectors** |
| — | **AI Agents** — digital workers |
| Store | **FactoryOS Store** |

Every customer runs the **same unchanging core**. What differs is the set of activated
plugins. Onboarding a new factory is configuration, not code.

## Principles

- **Everything is a plugin** — modules, connectors, agents, dashboards, reports.
- **Event-driven** — modules never call each other; they publish and subscribe to events.
- **Connector-gated** — the outside world (ERP, PLC, 3rd-party) is reached only through connectors.
- **Standard Model** — connectors normalize every source into one canonical language the AI and modules speak.
- **Multi-tenant** — every factory is an isolated tenant.
- **The core never changes for a customer** — only `plugins/` grows.

## Tech stack

- **Backend:** ASP.NET Core 10 (LTS), C#, Clean Architecture, DDD, CQRS, MediatR, EF Core, PostgreSQL
- **Messaging:** MassTransit + RabbitMQ · **Cache:** Redis · **Storage:** MinIO
- **Frontend:** React, TypeScript, Tailwind, PWA
- **AI:** LLM Gateway (OpenAI-compatible + Ollama), Embeddings, RAG, MCP, Agent Framework
- **IoT:** MQTT, OPC-UA, Modbus TCP/RTU, Siemens S7, Edge Gateway
- **Runtime:** Docker, Kubernetes (optional)

## Documentation

- [Engineering Constitution & Architecture](CLAUDE.md) — the rules every contributor obeys
- [Architecture overview](docs/architecture/overview.md)
- [Roadmap (40–50 sprints)](docs/ROADMAP.md)
- [Contracts](docs/contracts/) — event envelope, module, tenant manifests

## Status

Phase 0 (Core Platform) — bootstrapping.
