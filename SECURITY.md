# Security Policy

FactoryOS Enterprise runs the operational core of industrial facilities, so we treat
security as a first-class engineering concern. Thank you for helping keep it safe.

## Supported versions

The project is pre-1.0 and evolves sprint by sprint. Security fixes are applied to the
`main` branch (the `[Unreleased]` section of [CHANGELOG.md](CHANGELOG.md)). Until the
first tagged release, only `main` is supported.

| Version | Supported |
|---------|-----------|
| `main`  | ✅        |
| tagged releases | ⚠️ once the first release is cut, the latest minor is supported |

## Reporting a vulnerability

**Please do not open a public issue for security problems.**

Report privately to **security@factoryos.io** with:

- a description of the issue and its impact,
- the affected component (core, a plugin, a connector, an agent, the edge gateway or the web app),
- reproduction steps or a proof of concept,
- any suggested remediation.

We aim to acknowledge a report within **3 business days** and to provide a remediation
plan within **10 business days**. We will keep you informed through the fix and coordinate
disclosure once a patch is available. We do not currently operate a paid bounty program,
but we credit reporters who wish to be named.

## Handling secrets

FactoryOS never commits real secrets. Sample configuration files use `${secret:...}`
placeholders and are resolved from a secrets provider at runtime. If you discover a
committed credential, treat it as a vulnerability and report it privately as above — do
not include the secret value in any public channel.

## Scope

In scope: the FactoryOS core, first-party plugins, connectors, AI agents, the edge
gateway and the web application in this repository. Out of scope: third-party
dependencies (report those upstream) and issues that require a compromised host or
already-elevated privileges.
