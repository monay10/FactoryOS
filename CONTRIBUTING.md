# Contributing to FactoryOS Enterprise

Thanks for contributing. FactoryOS is an AI-native industrial operating system built on a
strict, non-negotiable architecture. Before you write a line, read the two documents that
govern every change:

- **[docs/CONSTITUTION.md](docs/CONSTITUTION.md)** — the immutable rules (ratified). It always wins.
- **[CLAUDE.md](CLAUDE.md)** — the architectural expression of those rules and the repository map.

If a task appears to require breaking a rule, **stop and raise it** — do not work around it.

## Architecture in one screen

- **Modular core.** The core contains no customer-specific code, ever. No `if (tenant == "ABC")`.
- **Everything is a plugin.** Features, connectors, AI agents, dashboards — all live under
  `plugins/`, `connectors/`, `agents/`, discovered by manifest (`module.json`, `connector.json`,
  `agent.json`), never by name.
- **Connectors are the only door to the outside.** No business module talks to an ERP/PLC directly.
- **Event-driven only.** Modules never reference each other; they publish and subscribe to events
  on the bus, in the Standard Model. Consumers are idempotent (dedupe by event id); the tenant is
  always in scope.
- **Layered separation.** API, application/service and UI layers stay separated; the Dependency
  Rule points inward (Core knows no Infrastructure).

## Technology & layout

- Backend: **.NET 10 / ASP.NET Core 10 (LTS)**, Clean Architecture, DDD, CQRS with MediatR.
- Frontend: **React + TypeScript + Tailwind** (PWA) under `web/`.
- Solution of record: **`FactoryOS.slnx`** at the repository root.
- See [CLAUDE.md §4](CLAUDE.md) for the full repository layout.

## Getting started

```bash
# Backend — build & test (warnings are errors; 0 warnings / 0 errors is the bar).
dotnet build FactoryOS.slnx -c Release
dotnet test  FactoryOS.slnx -c Release --no-build

# Frontend.
cd web && npm ci && npx tsc --noEmit && npm test && npm run build

# Full dev stack.
docker compose up
```

## Coding standards

- Match the surrounding code: comment density, naming, and idiom.
- `nullable` and implicit usings are on; file-scoped namespaces; `var` per the `.editorconfig`.
- Public members are documented (XML docs) — the build treats missing docs as an error.
- No mocks, demo code, or `TODO`s in committed source. Ship complete, compiling code.
- Never commit secrets. Sample configs use `${secret:...}` placeholders only.

## Definition of Done (every change)

- [ ] Conforms to all rules in the Constitution and CLAUDE.md §1.
- [ ] Automated tests pass (unit + integration where I/O is involved).
- [ ] Documentation updated.
- [ ] A sample configuration file is provided for any new feature.
- [ ] Runs under `docker compose` in the dev profile.
- [ ] A newest-first entry is added to the `[Unreleased]` section of [CHANGELOG.md](CHANGELOG.md).

## Adding a plugin, connector or agent

Prefer a **new plugin over a core change**. Each is self-contained (see the layout in
CLAUDE.md §4): a manifest, its API/application/domain, events, rules, migrations, **tests**, and a
`sample.config.json`. Deleting the folder must remove the feature with zero core diff.

## Commit & PR conventions

- Branch off `main`; keep changes focused and reviewable.
- Write a clear description of *what* and *why*; link the sprint or issue.
- Ensure the backend build is green (0 warnings / 0 errors) and all tests pass before opening a PR.
- Security issues follow [SECURITY.md](SECURITY.md), not public PRs/issues.
