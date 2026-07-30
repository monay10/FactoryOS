# Platform Composition (Commit 0023)

This folder is the **composition root** for the platform: the one place the running API host stands up the
platform engines and runtimes and wires them together. It is called once, from `Program.cs`:

```csharp
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddPlatformEngines(builder.Configuration)   // <-- here
    .AddPluginModules(pluginsRoot)
    ...
```

## Why this exists

Through Commits 0014–0022 the platform engines (Security, Audit, Monitoring, Approval, SLA, Notification,
Human Task, Forms, Workflow) and the two runtimes (Connector, Plugin) were each built and tested as **library
code**. Nothing composed them into a running process — the deployed host loaded no plugins and constructed no
engine. They were live only inside tests.

`AddPlatformEngines` closes that gap. After it runs, every engine and both runtimes are resolvable from the
same container the request pipeline uses. `GET /platform/status` proves it, component by component.

## What it does, in order

1. **Registers each engine** through its own self-contained `Add<Engine>Engine()` extension. Every registration
   is idempotent (`TryAdd`) and backed by in-memory stores, so the host starts without a database.
2. **Registers the opt-in cross-engine integrations** — security decisions flow into the audit trail and are
   measured; SLA breaches raise notifications — exactly as a deployment would choose them.
3. **Binds the plugin runtime's ports to the engines** (`PlatformPluginAdapters`): `IPluginAuthorizer` →
   security engine, `IPluginAuditSink` → audit engine, `IPluginMetricSink` → monitoring engine. These are
   registered **before** `AddPluginRuntime`, so the runtime's in-memory `TryAdd` defaults defer to them and a
   plugin's authorization, audit and metrics run through the platform.
4. **Registers the two runtimes** (`AddConnectorRuntime`, `AddPluginRuntime`).
5. **Registers a `platform` readiness check** that resolves every component and reports each up or down.

## The one architectural tension, stated plainly

The API host references the `FactoryOS.Plugins.Workflow` assembly to compose the platform engines. Strictly,
the Constitution's modular-monolith design has first-party features discovered by manifest and loaded via an
`AssemblyLoadContext`, and the core host referencing a plugin by name sits in tension with that.

It is tolerated here for one reason: **Security, Audit and Monitoring are platform services that happen to be
packaged inside `plugins/workflow/`.** Composing them from the composition root is composition-root behavior,
not customer code — nothing here branches on a tenant. This is not a licence to reach into a business plugin.

The proper resolution is to **relocate the platform engines into a dedicated Platform assembly** the host may
reference without tension. That is a separate, larger refactor (it moves code the engine commits were told not
to change) and is intentionally out of this commit's scope.

## What this commit is not

No per-engine HTTP endpoints, no management screens, no persistence to Postgres/RabbitMQ, and no deployment of
the workflow plugin's automation handlers. Those build on this foundation as their own commits. This commit is
the composition: the engines now run in the process, and that is provable.
