# Plugin Runtime (Commit 0022)

The layer that lets a factory **install, run, update and remove** a plugin at run time — signed, isolated,
permitted, and belonging to exactly one tenant.

> **Where it lives.** Per Commit 0022's rules, **no new project was created and the existing plugin framework
> was not changed.** The runtime lives inside `src/FactoryOS.Plugin/` under `PluginRuntime/`, in the
> `FactoryOS.Plugins.Runtime.*` namespace, and builds on the framework beside it.
>
> The folder is `PluginRuntime/` rather than `Runtime/` because the framework already owns `Runtime/` — its
> `FactoryOS.Plugin.Runtime` namespace holds `PluginDescriptor`, `PluginContext`, `PluginMetadata` and
> `PluginCapability`. This layer is the **plural** `FactoryOS.Plugins.Runtime`, and the two never share a
> folder.

## What was already here, and what this adds

`src/FactoryOS.Plugin/` already had a great deal, and **none of it was rebuilt**:

| Already present | Where |
|---|---|
| `IPlugin`, `PluginBase`, `PluginManifest`, `PluginVersion`, `PluginDependency`, `PluginState` | `FactoryOS.Contracts.Plugins` |
| `PluginDescriptor`, `PluginContext`, `PluginMetadata`, `PluginCapability` | `Runtime/` |
| `PluginRegistry`, `PluginHost`, `PluginManager`, `PluginActivator`, `PluginCatalog` | `Registry/`, `Hosting/`, `Management/`, `Activation/`, `Catalog/` |
| `PluginManifestReader`, `PluginDiscovery`, `PluginDependencyResolver` | `Manifest/`, `Discovery/`, `Dependencies/` |
| `PluginLoadContext` (collectible, contract-unifying) | `Isolation/` |
| `PluginHealthService` (heartbeats, failure latch, recovery) | `Health/` |
| `PluginConfiguration`, `PluginOptions` | `Configuration/` |

What was missing is everything that only exists once a plugin can be managed **per tenant, at run time**:

```
PluginPackage    — one installable unit: a manifest, its projection, and the signature vouching for it
PluginDefinition — what a plugin kind is, and what it contributes                (shared by every tenant)
PluginInstance   — one factory's installation: version, settings, grants, state  (owned by one tenant)
PluginExtension  — one resolved contribution to one published extension point
```

Plus: a signature to verify, a lifecycle that includes suspend, update, rollback and remove, a sandbox, an
extension-point catalogue, and per-tenant isolation of configuration, storage, permissions and resources.

## The framework can load a plugin. It cannot unload one.

`ModuleLoader.Load` creates a collectible `PluginLoadContext`, loads the entry assembly, and **lets the
context go**. Nothing holds a reference, so nothing can ever ask it to unload. That is fine for a monolith
that loads once at boot; it is the whole problem for a runtime that has to update, roll back and remove.

`PluginIsolationManager` keeps that reference, per `tenant|plugin`. That single change is what makes the rest
of this commit possible, and it is why the runtime owns its own loading path rather than calling the
framework's — the entry-type rule is deliberately identical, and activation is still delegated to the
framework's `IPluginActivator` so the key check lives in exactly one place.

The honest limit: requesting an unload *begins* a collection that completes only once nothing references
anything the context loaded. The runtime reports that the release was **requested** and never claims the
assembly is gone.

## The signature is verified before the code is loaded

You cannot un-run code. A check performed after an assembly is in the process has already lost, whatever it
decides. So installation always precedes loading, and validation runs in one fixed order:

| # | Check | Why it sits there |
|---|---|---|
| 1 | **Manifest** | Is the package even coherent — key, name, no self-dependency, no duplicate capability? |
| 2 | **Signature** | Before anything acts on the package's claims, and before a byte of it is loaded |
| 3 | **Compatibility** | Will it run on this platform version at all? |
| 4 | **Capabilities** | Is what it needs provided by something this tenant runs? |
| 5 | **Permissions** | Has this tenant granted what it asks for? |

The permission check is **last** on purpose: it is the only step whose answer is a property of the tenant
rather than of the package. Refusing an unsigned or incompatible package with "you have not granted it
enough" sends an operator to the wrong screen entirely.

Two rules about signatures are worth stating plainly:

- An **invalid** signature is always fatal, whatever the configuration says. A package claiming to be signed
  and failing to prove it is a worse signal than an unsigned one.
- An **absent** signature is fatal only when the host sets `RequireSignature`. First-party packages compiled
  into the monolith ship unsigned; a Store package must never be trusted unsigned.

The signature covers the manifest's **claims** — key, version, entry type, isolation, contributions and
requested permissions — not just the assembly name. A signature over the bytes alone would still let someone
re-point a verified assembly at a different entry type, or widen the permissions it asks for.

Signing keys are a **port** (`IPluginSigningKeySource`). They belong to the identity and secret-management
layers; the default reads them from the environment so a development host works, and a real deployment
substitutes a vault. No key is ever written into a manifest, a package or a sample config.

## Permissions are a ceiling intersected with a grant

A manifest declares what a plugin **asks for**. A tenant declares what it **grants**. What the plugin may
actually do is the intersection, and both halves matter: it can never exceed its own manifest, so its reach
is auditable from the package alone; and it can never exceed the tenant's grant, so a factory is never
surprised by a permission an update quietly added.

Contributing to an extension point **implies** asking to extend it. A plugin with a UI screen in its manifest
necessarily requests `uimetadata.extend`, whether or not it spelled the permission out — so a contribution
can never smuggle in reach the tenant refused.

The grant is re-checked at **start**, not only at install. A permission revoked while a plugin was stopped
must not come back with it. And a grant cannot be narrowed out from under a *running* plugin: that request is
refused with an instruction to stop it first, rather than leaving a plugin running with reach the tenant has
just withdrawn.

## Extension points: the only way in

The catalogue is **closed**, published by the platform, and contributed to by plugins — never the other way
round:

`workflow` · `forms` · `humantask` · `approval` · `notification` · `sla` · `audit` · `monitoring` ·
`security` · `connector` · `api` · `uimetadata` · `rules` · `ai` · `reporting`

A plugin cannot invent a point; contributing to an unknown one is a manifest validation failure. That is what
stops a plugin reaching into an engine's internals and calling it an extension.

An engine asks `IPluginRuntime.Extensions(tenant, point)` and gets back **data** — a name, a description and
the plugin's own reference. The runtime never loads or invokes a contribution, which is exactly why it can be
built, tested and shipped without depending on a single engine.

**Only a running instance contributes.** A stopped, suspended, failed or disabled plugin is withdrawn from the
extension surface rather than left listed, because an engine that resolves a contribution expects to be able
to use it.

## Lifecycle

```
Install → Load → Start ⇄ Suspend/Resume → Stop → Unload → Remove
                    └──────── Update / Rollback ────────┘
```

- **Suspend is not stop.** A suspended plugin keeps its state and its loaded assemblies and refuses new work;
  that is why resuming is instant. Only a running plugin can be suspended, and only a suspended one resumed.
- **An update always stops first.** Replacing the code under a running plugin is how a half-updated process is
  produced. The update stops, unloads, installs, reloads, and starts again only if it was running before.
- **A rollback needs somewhere to go.** The superseded package must still be retained; if `RetainedVersions`
  dropped it, the runtime says so plainly rather than reporting a rollback that silently did nothing.
- **Removing a plugin from a tenant keeps the package.** It belongs to the platform, not to the factory that
  happened to remove it, and another factory may still be running it.

## Tenant isolation is structural

An instance's identity **is** `tenant|plugin`. There is no lookup that would return another factory's plugin
— reaching one is not refused by a check somebody could remove, it is unreachable.

On top of that, seven things are separated per tenant: **dependencies** (its own load context),
**configuration** (its own settings section), **storage** (its own directory), **permissions** (its own
effective set), **tenancy** (the identity itself), **resources** (its own quota) and **version** (two
factories may run different versions of one plugin side by side).

The storage path carries the tenant and the plugin but deliberately **not the version**: a plugin's data has
to survive the update that changes its code, and a rollback has to find what the newer version left behind.

The tenant gate lives in `PluginAuthorizationGate`, applied **before** `IPluginAuthorizer` is consulted. That
port is replaceable, and an adapter forwarding to a decision layer that only ever sees the *caller* cannot
know which tenant the *instance* belongs to. A port may decide permissions; it may not decide tenancy. An
authorizer that allows everything still cannot get a request across a tenant boundary.

## The sandbox

A sandboxed plugin passes three questions every time it acts: is it running, is it permitted, and is it
within its quota. All three are asked per **instance**, so exhausting a quota degrades one factory rather
than every factory that happens to run the same plugin. A granted lease returns its concurrency slot on
dispose, so a plugin that leaks leases throttles itself and not the host.

## Health: six questions, not one

`PluginHealthEngine` asks **liveness, readiness, dependencies, version, permissions and resources**
separately and reports the worst answer. A plugin can be beating happily while the tenant has revoked a
permission it requires, or while the plugin it depends on is stopped.

**Silence is not health.** A plugin that has never been started reports `Unknown`, not `Healthy`. And a plugin
an operator deliberately switched off reports `Unknown` rather than a fault — an alarm about something that
is exactly as intended is an alarm people learn to ignore.

*Known limitation.* The **readiness** aspect asks the framework's `IPluginHealthService`, which is keyed by
plugin key alone and is therefore process-wide rather than per tenant. Every other aspect is per instance.
Narrowing the framework's heartbeat to a tenant needs a commit permitted to change the framework, and this one
was not.

## Security, audit and monitoring are ports

The runtime has no reference to the platform's security, audit or monitoring engines — it cannot see them,
and that is the design. It states what happened in its own vocabulary and a host adapter maps it:

| Port | Default | What a host substitutes |
|---|---|---|
| `IPluginAuthorizer` | the permissions the caller arrived holding | the platform's security engine |
| `IPluginAuditSink` | bounded in-memory trail | the platform's audit engine |
| `IPluginMetricSink` | bounded in-memory series | the platform's monitoring engine |
| `IPluginRuntimeEventSink` | bounded in-memory history | the event bus, a SIEM forwarder |
| `IPluginSigningKeySource` | environment variables | a vault |

Every one of them **fans out** to all registered subscribers rather than allowing a single consumer.

Events, audit lines and measurements are all derived from one `PluginTelemetry`, so the three can never
disagree about what happened. A **failure is always audited** even when the host has switched off auditing of
successes: turning down audit noise is legitimate, losing the record of the install that did not happen is
not.

## Registering, and configuration only

```csharp
services.AddPluginRuntime(configuration);   // binds Plugins and Plugins:Runtime
```

Onboarding a factory to a plugin is a `PluginInstance` — a tenant, a package, a grant, some settings — and
nothing else. No core code path names a customer, and none names a plugin.

## What is not configurable

There is no setting that disables the permission check, none that lets a request reach another tenant's
plugin, and none that accepts an invalid signature. Those are invariants, not policy.

## Out of scope for this commit

No Store front-end, no package download or transport, no out-of-process plugin hosting, no management
screens, no database-backed stores. This commit is the **runtime**: discovery, packaging, signature,
installation, loading, isolation, lifecycle, extension points, health and the infrastructure underneath. The
ports above are deliberately the shape those integrations plug into.

## Tests

- **Unit** — `tests/FactoryOS.Tests/Plugins/PluginRuntimeTests.cs`: permissions, extension points, manifests,
  packages and signatures, discovery, version and dependency resolution, compatibility, the full lifecycle,
  update and rollback, configuration, isolation, the sandbox, health, scheduling, the host and the cold start.
- **Integration** — `tests/FactoryOS.IntegrationTests/Plugins/PluginRuntimeIntegrationTests.cs`: the whole
  container, the repository's **real sample plugin assembly loaded from disk** and unloaded again, signature
  verification of a staged package, update and rollback, and the runtime's ports wired to the platform's real
  security, audit and monitoring engines — plus a plugin contributing a connector that the connector runtime
  registers from data alone.
