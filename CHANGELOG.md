# Changelog

All notable changes to FactoryOS Enterprise are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/); the project adheres to
Semantic Versioning. Per the [Constitution](docs/CONSTITUTION.md) (rule 26), every sprint
appends an entry.

## [Unreleased]

### Commit 0011 — Connector framework (2026-07-21)

Added
- **`FactoryOS.Connectors`** — completed the connector platform on top of the existing normalize/dedup ingestion
  pipeline (binder, deduplicator, normalizer, transforms, `ConnectorManifestReader`). Only the Connectors project
  changed. Existing abstractions were **reused, not duplicated**: the connector code contract `IConnector` and the
  `ConnectorManifest` (both in `FactoryOS.Contracts`) are reused as-is — no parallel connector interface was created —
  and `AddConnectorFramework()` was extended rather than replaced. New additions (under `Framework/`):
  - **Configuration**: `ConnectorConstants`, `ConnectorOptions` with nested `ConnectorDiscoveryOptions`,
    `ConnectorHealthOptions` and `ConnectorSecurityOptions` (bound from `Connectors`, `Connectors:Discovery`,
    `Connectors:Health`, `Connectors:Security`); `ConnectorConfiguration` + `IConnectorConfigurationProvider`/
    `ConnectorConfigurationProvider` reading `Connectors:Configuration:<key>` (with an `Enabled` toggle and on-demand
    secret decryption); `IConnectorSecretProtector` with an AES-GCM implementation (`enc:` prefixed) and a
    development passthrough — the encryption key is supplied out-of-band, never committed.
  - **Runtime models**: `ConnectorVersion` (ordered semver), `ConnectorCapability` (a `[Flags]` capability surface —
    Read, Write, Events, Commands, Files, Streaming — with `Supports`/`Parse`), `ConnectorState`,
    `ConnectorDescriptor` (reusing the contract manifest plus a declared version/capabilities), `ConnectorMetadata`,
    `IConnectorContext`/`ConnectorContext`.
  - **Lifecycle** (`Lifecycle/IConnectorLifecycle.cs`): the optional `InitializeAsync`/`ConnectAsync`/`DisconnectAsync`
    plus `IAsyncDisposable` extension to the `IConnector` read contract.
  - **Activation**: `IConnectorActivator`/`ConnectorActivator` (in-process type activation with key verification).
  - **Health**: `ConnectorHealthStatus`, `ConnectorHealth`, `IConnectorHealthCheck`,
    `IConnectorHealthService`/`ConnectorHealthService` (heartbeat, failure detection via staleness and a counter,
    recovery detection).
  - **Registry/Catalog/Management/Hosting**: `IConnectorRegistry`/`ConnectorRegistry`,
    `IConnectorCatalog`/`ConnectorCatalog` (metadata, capability index, health), `IConnectorManager`/`ConnectorManager`
    driving Initialize → Connect → Disconnect → Reconnect → Dispose, and `IConnectorHost`/`ConnectorHost` bulk
    connect/disconnect over enabled connectors.
  - **DI** (`DependencyInjection.cs`): `AddConnectorFramework()` now also registers the registry, activator,
    configuration provider, secret protector (AES when a key is configured, otherwise passthrough), health service,
    catalog, manager and host; a new `AddConnectorFramework(IConfiguration)` overload binds `ConnectorOptions`. The
    csproj gained the configuration/options packages.
- **Tests** — 12 unit tests (`FactoryOS.Tests/Connectors/ConnectorFrameworkFoundationTests.cs`: version ordering,
  capability flags, manifest→descriptor→metadata reuse, AES round-trip + passthrough + configuration secret
  decryption, activation + key enforcement, health heartbeat/failure/recovery/staleness, full connection lifecycle,
  unknown/unattached handling, catalog projection, DI resolution + protector selection) and 2 integration tests
  (`FactoryOS.IntegrationTests/Connectors/`: multiple connectors connect/reconnect/disconnect through the host with
  catalog + health, and an encrypted per-connector secret decrypted with the configured key).

### Commit 0010 — Plugin framework (2026-07-20)

Added
- **`FactoryOS.Plugin`** — completed the plugin framework on top of the substantial framework that already existed
  (`IPlugin`/`PluginBase`, `PluginManifest`/`PluginVersion`/`PluginDependency`/`PluginState`, `PluginDescriptor`,
  `PluginRegistry`, `PluginDiscovery`, `ModuleLoader`, `PluginHost`, `PluginAdmin`, `PluginManifestReader`,
  `PluginDependencyResolver`, `PluginLoadContext`). Only the Plugin project changed. Existing abstractions were
  **reused, not duplicated**: the spec's `IPluginLoader`/`PluginLoader` are the existing `IModuleLoader`/`ModuleLoader`,
  and the registry, host, discovery, descriptor, dependency resolver and version are unchanged. New additions:
  - **Configuration** (`Configuration/`): `PluginConstants`, `PluginOptions` with nested `PluginDiscoveryOptions`,
    `PluginCatalogOptions` and `PluginHealthOptions` (bound from `Plugins`, `Plugins:Discovery`, `Plugins:Catalog`);
    `PluginConfiguration` + `IPluginConfigurationProvider`/`PluginConfigurationProvider` reading
    `Plugins:Configuration:<key>` (with an `Enabled` toggle).
  - **Runtime models** (`Runtime/`): `PluginMetadata` (manifest projection), `PluginCapability` +
    `PluginCapabilityValidator` (capability validation over the manifest `provides` surface),
    `IPluginContext`/`PluginContext`.
  - **Lifecycle** (`Lifecycle/IPluginLifecycle.cs`): the optional `InitializeAsync`/`UnloadAsync` extension to the core
    `IPlugin` start/stop contract.
  - **Activation** (`Activation/`): `IPluginActivator`/`PluginActivator` (in-process type activation with key
    verification — no external DLL loading).
  - **Health** (`Health/`): `PluginHealthStatus`, `PluginHealth`, `IPluginHealthCheck`,
    `IPluginHealthService`/`PluginHealthService` (heartbeat, failure detection via staleness and a failure counter,
    and a `Recovered` notification).
  - **Management** (`Management/`): `IPluginManager`/`PluginManager` driving Initialize → Start → Stop → Unload →
    Reload over the registry descriptors (in-process; Reload is stop-then-start).
  - **Catalog** (`Catalog/`): `IPluginCatalog`/`PluginCatalog` projecting metadata, a capability index and health.
  - **DI** (`DependencyInjection.cs`): `AddPluginFramework()` now also registers the activator, configuration provider,
    health service, catalog and manager; a new `AddPluginFramework(IConfiguration)` overload binds `PluginOptions`.
    Added `PluginDescriptor.MarkStopped()` (started → loaded). The Plugin csproj gained the configuration/options
    packages.
- **Tests** — 11 unit tests (`FactoryOS.Tests/Plugins/PluginFrameworkFoundationTests.cs`: options defaults,
  per-plugin configuration, metadata projection, capability validation, activation + key enforcement, health
  heartbeat/failure/recovery/staleness, full manager lifecycle, unknown/unloaded handling, catalog projection, DI
  resolution) and 2 integration tests (`FactoryOS.IntegrationTests/Plugins/`: multiple plugins start/reload/stop
  through the manager with catalog + health, and per-plugin configuration read from the host).

### Commit 0009 — Authorization foundation (2026-07-20)

Added
- **`FactoryOS.Identity` (Authorization component)** — completed the authorization foundation. There is no standalone
  Authorization project; the authorization surface lives in `FactoryOS.Identity.Authorization` (the existing
  `Permission`, `AuthorizationPolicy` and `PermissionAuthorizer`), so the foundation was built there, **reusing** the
  existing `Permission` and `AuthorizationPolicy` models rather than duplicating them and without introducing a new
  project (which would change the architecture):
  - **Configuration** (`Authorization/Configuration/`): `AuthorizationConstants`, `AuthorizationOptions`
    (`EnableRoleInheritance`), `PermissionCacheOptions`, `PolicySettings` — bound from `Authorization`,
    `Authorization:PermissionCache` and `Authorization:Policies`.
  - **Models** (`Authorization/Model/`): `PermissionGroup`, `PermissionDefinition`, `PermissionAssignment` +
    `RolePermission`/`UserPermission`, the `AuthorizationRequirement` hierarchy
    (`PermissionRequirement`/`RoleRequirement`/`PolicyRequirement`) and `AuthorizationResult`.
  - **Context** (`Authorization/Context/`): `AuthorizationContext` (user/tenant/roles/permissions),
    `IAuthorizationContextAccessor`/`AuthorizationContextAccessor` mapping the scoped `IdentityContext`'s claims.
  - **Evaluation** (`Authorization/Evaluation/`): `IPermissionEvaluator`/`PermissionEvaluator` with wildcard
    (`*`, `energy.*`) and hierarchical (`energy` grants `energy.read`) matching.
  - **Services** (`Authorization/Services/`): `IRoleService`/`RoleService` (cycle-safe transitive role inheritance),
    `IPermissionService`/`PermissionService` (catalog, role/user grants and denials, effective-permission resolution),
    `IPolicyProvider`/`PolicyProvider` (config-seeded + runtime policies), and `IAuthorizationService`/
    `AuthorizationService` (handler dispatch).
  - **Handlers** (`Authorization/Handlers/`): `IAuthorizationHandler` + `PermissionAuthorizationHandler`,
    `RoleAuthorizationHandler`, `PolicyAuthorizationHandler`.
  - **Caching** (`Authorization/Caching/`): `IAuthorizationCache`/`InMemoryAuthorizationCache` (TTL-bounded, shared by
    the permission, role and policy services; bypassed when disabled).
  - **DI** (`Authorization/Foundation/AuthorizationFoundation.cs`): `AddAuthorizationFoundation()` binds the section and
    registers the cache, evaluator, role/permission/policy services, the three handlers (`TryAddEnumerable`), the
    authorization service and the context accessor.
- **Tests** — 21 unit tests (`FactoryOS.Tests/Authorization/`: wildcard + hierarchical evaluation, transitive/cycle-safe
  role inheritance, cache TTL/disable/invalidate, permission resolution with denials, policy seeding, each handler,
  service dispatch, DI resolution) and 2 integration tests (`FactoryOS.IntegrationTests/Authorization/`: claims →
  context mapping enforcing permissions/policies/roles end-to-end, and an anonymous principal).

### Commit 0008 — Identity foundation (2026-07-20)

Added
- **`FactoryOS.Identity`** — completed the identity foundation on top of the substantial layer that already existed
  (`User`/`Role`/`Permission`/`Tenant`/`Organization`, `Pbkdf2PasswordHasher`, `JwtAccessTokenService`,
  `RefreshTokenService`, `PermissionAuthorizer`, `ClaimsFactory`, in-memory stores, seeder). Only the Identity project
  changed. Existing abstractions were **reused, not duplicated**: `User`/`Role`/`Permission` serve as the application
  user/role/permission, `IAccessTokenService`/`JwtAccessTokenService` as the token service, and
  `IPasswordHasher`/`Pbkdf2PasswordHasher` as the password hasher — no parallel `ITokenService`/`PasswordHasher` was
  introduced. New additions:
  - **Configuration** (`Configuration/`): `IdentityConstants`, `IdentityOptions` with nested `PasswordPolicyOptions`,
    `LockoutOptions` and `SessionOptions` (bound from `Identity`, `Identity:PasswordPolicy`, `Identity:Session`).
  - **Context & accessors** (`Context/`, `Execution/`): scoped `IdentityContext` (initialize-once ambient principal;
    tenant always in scope), `ICurrentPrincipalAccessor`/`CurrentPrincipalAccessor`,
    `ICurrentClaimsAccessor`/`CurrentClaimsAccessor`; `ApplicationClaim` claim descriptor and a `Session` claim type.
  - **Password policy** (`Policies/`): `IPasswordPolicy`/`PasswordPolicyValidator` (minimum length, uppercase,
    lowercase, digit, non-alphanumeric).
  - **Account lockout** (`Lockout/`): `IAccountLockoutService`/`AccountLockoutService`, `ILoginAttemptStore` +
    in-memory store, `LockoutState` (threshold-based lock, timed unlock, reset on success, inert when disabled).
  - **Sessions** (`Sessions/`): `ApplicationSession` (sliding idle + absolute timeout), `ISessionStore` + in-memory
    store, `ISessionService`/`SessionService` (create, validate, touch, revoke, revoke-all-for-user).
  - **Identity façade** (`Services/`): `IIdentityService`/`IdentityService` (register user with policy + uniqueness +
    tenant checks, change password, and permission/role/claim resolution) composing the existing stores, hasher and
    claim factory.
  - **JWT** (`Tokens/`): added configurable `ClockSkewSeconds` to `JwtOptions`, applied by `JwtAccessTokenService`
    (default `0` preserves exact expiry). Issuer, audience, signing credentials and expiration remain as before.
  - **DI** (`Foundation/IdentityFoundation.cs`): `AddIdentityFoundation()` binds the identity/password-policy/session
    sections and registers the context, accessors, password policy, lockout, sessions and identity façade on top of the
    base `AddIdentityModule` (idempotent via `TryAdd`).
- **Tests** — 21 unit tests (`FactoryOS.Tests/Identity/IdentityFoundationTests.cs`: DI resolution, password policy,
  lockout lifecycle, session idle/absolute expiry + revocation, user registration/resolution, identity context and
  current accessors) and 3 integration tests (`FactoryOS.IntegrationTests/Identity/`: a registered user's JWT validates
  back into a tenant/permission-carrying principal, session create/validate/revoke through the wired service, and
  refresh-token issue/validate/rotate with replay rejection).

### Commit 0007 — Persistence foundation (2026-07-20)

Added
- **`FactoryOS.Persistence`** — completed the persistence foundation, reconciling with the substantial layer that
  already existed (write repository, combined auditing interceptor, abstract context, unit of work, initializer,
  providers) rather than duplicating it. Only the Persistence project (production) changed. Provider reconciliation:
  **PostgreSQL** (production, per the Constitution) + **SQLite** (development/tests) — SQL Server was not adopted
  (it would break the locked stack and is unavailable offline):
  - **Configuration** (`Configuration/PersistenceOptions.cs`): `PersistenceConstants`, `PersistenceOptions`
    (`Provider`, connection string, command timeout, retry, migrations assembly, dev flags).
  - **Value converters** (`ValueConversion/`): `MoneyConverter`, `PercentageConverter`, `DateRangeConverter`,
    `PeriodConverter`, `EnumerationConverter<T>`, `UtcDateTimeConverter`, and a converter per strongly-typed identifier
    (`TenantId`, `UserId`, `MachineId`, `FactoryId`, `OrganizationId`, `PlantId`, `LineId`, `WorkCenterId`,
    `CorrelationId`).
  - **Conventions** (`Conventions/FactoryOsConventions.cs`, applied by `FactoryOsDbContext.ConfigureConventions`):
    UTC `DateTime`, `decimal(18,2)` precision, and the strongly-typed-id conversions — all additive and override-able,
    so existing module models are unaffected.
  - **Configurations** (`Configuration/`): `BaseEntityConfiguration<TEntity,TId>` (key + audit-column lengths + hook),
    `BaseValueObjectConfiguration<T>`, and `EntityConfigurationDiscovery` (`ApplyConfigurationsFrom<TMarker>`/assembly).
  - **Read side** (`Repositories/ReadRepository.cs`): `IReadRepository<TAggregate,TId>` + `ReadRepository` (tracking-free
    `GetById`/`List`/`Count`/`Any`), complementing the existing write `EfRepository`.
  - **Migrations** (`Migrations/DatabaseMigrator.cs`): `MigrationAssemblyResolver`, `IDatabaseMigrator`/`DatabaseMigrator`.
  - **DI** (`PersistenceRegistration.cs`): `AddFactoryOsDbContext<TContext>` (registers the context, the base-`DbContext`
    bridge, generic `IRepository`/`IReadRepository`/`IUnitOfWork`, and the auditing interceptor) and
    `UseFactoryOsDatabase<TContext>` (SQLite/PostgreSQL selection, timeout, migrations assembly + history table, retry,
    dev diagnostics). `AddPersistence` now binds `PersistenceOptions` and registers `IDatabaseMigrator`.
- **Tests** — 11 unit tests (`FactoryOS.Tests/Persistence/`: value-converter round-trips, options defaults, migration
  resolver) and 7 integration tests (`FactoryOS.IntegrationTests/Persistence/`: value objects + strongly-typed id
  round-trip on SQLite, tracking-free reads, convention model checks, provider selection, `AddFactoryOsDbContext`
  composition + CRUD).

Changed
- **`EfUnitOfWork`** now implements both the domain and shared-kernel `IUnitOfWork` (identical signature), so either
  abstraction — including the Commit 0006 `TransactionManager` — resolves to it once a context is registered.
- **`FactoryOsDbContext`** gained a `ConfigureConventions` override applying the platform conventions.

Notes
- **Not duplicated**: audit, soft-delete and concurrency remain in the existing `AuditingSaveChangesInterceptor`
  (one cohesive interceptor); the write repository and unit of work were reused, not re-created. Value-object
  converters (Money/DateRange/…) are opt-in per entity — not global conventions — to avoid disturbing modules that map
  them differently.
- Architecture impact: only the Persistence project (plus the two test projects) changed. Build green (0/0); .NET
  tests 933 → 951. SQLite provider, database initialization and CRUD verified in integration tests.

### Commit 0006 — ASP.NET Core host foundation (2026-07-20)

Added
- **`FactoryOS.Api`** — the cross-cutting HTTP host foundation, wired without touching any business module (no
  controllers, no auth, no database). Offline constraint: OpenAPI/Swagger and API versioning are implemented on
  framework primitives (the Swashbuckle/Asp.Versioning packages are unavailable in this environment):
  - **Middleware** (`Middleware/HostMiddleware.cs`): `GlobalExceptionMiddleware` (maps the FactoryOS domain-exception
    family to RFC 7807 problem details), `CorrelationIdMiddleware` (`X-Correlation-Id` in/out + log scope),
    `RequestTimingMiddleware` (`X-Response-Time-ms`), `RequestLoggingMiddleware`, `CultureMiddleware`.
  - **Health**: `HostSelfHealthCheck` + `GET /health`, `/health/live`, `/health/ready` (tag-filtered, JSON writer).
  - **OpenAPI/Swagger**: `OpenApiDocumentFactory` serves a real OpenAPI 3.0.1 document at `/openapi/v1.json` and a
    Swagger UI at `/swagger`; document version tracks the default API version.
  - **API versioning foundation** (`Hosting/ApiVersioning.cs`): `ApiVersion` (parse/try-parse), `IApiVersionReader`/
    `ApiVersionReader` (header → query → default).
  - **ProblemDetails**: `AddProblemDetails` with a customizer that stamps `traceId`/`correlationId` on every problem.
  - **Request localization** (supported cultures + default), **named CORS policy**, **response compression**,
    **HTTP logging** — all bound from configuration.
  - **Options** (`Hosting/ApiFoundationOptions.cs`): `CorsSettings`, `LocalizationSettings`, `ApiVersioningSettings`,
    `OpenApiSettings`, bound from the `Cors`/`Localization`/`ApiVersioning`/`OpenApi` sections.
  - **DI/pipeline** (`Hosting/ApiHostFoundation.cs`): `AddApiHostFoundation` / `UseApiHostFoundation` /
    `MapApiHostFoundation`, composed into `Program.cs` around the existing Application, Infrastructure, plugin and
    gateway wiring; the old inline `/health` map was replaced by the health-check endpoints. Scoped `.editorconfig`
    disables CA1848/CA1873 (readability-first host logging).
  - **Configuration**: `appsettings.json` / `appsettings.Development.json` gained `Logging`, `Localization`, `Cors`,
    `ApiVersioning`, `OpenApi` and `Infrastructure` sections. No secrets are embedded (JWT signing key stays external).
- **Tests** — 20 host tests in `FactoryOS.IntegrationTests/Api/` (a TestServer host exercising health/live/ready,
  OpenAPI, Swagger, correlation + timing headers, and validation/not-found/unexpected → problem-details mapping) plus
  version/OpenAPI/culture unit checks.

Fixed
- **`TransactionManager` (Commit 0005)** now takes an optional `IEnumerable<IUnitOfWork>` instead of a hard
  `IUnitOfWork` dependency, so the host composes under DI validation (`ValidateOnBuild` in Development) when no
  persistence unit of work is registered; `BeginAsync` throws a clear error if used without one. This unblocked host
  startup, verified live: `GET /health`, `/health/live`, `/health/ready`, `/openapi/v1.json` and `/swagger` all
  respond on a running host.

Notes
- Architecture impact: only the Api project (production) changed, plus the Infrastructure `TransactionManager`
  correction and the two test projects. Build green (0/0); .NET tests 912 → 933.

### Commit 0005 — Infrastructure foundation (2026-07-20)

Added
- **`FactoryOS.Infrastructure`** — concrete implementations of the Application-layer abstractions (the project
  previously held only its `AddInfrastructure` composition root). All new; only the Infrastructure project and the
  unit-test project changed; no duplicate abstractions; `.NET 10`:
  - **Configuration**: `InfrastructureConstants`, `InfrastructureOptions` (bindable), `InfrastructureContext` — the
    scoped, ambient execution/security context populated once at the composition edge.
  - **Clock**: `SystemClock` implements `IApplicationClock` (single UTC source for `UtcNow` and `Today`).
  - **Current context**: `CurrentUser`, `CurrentTenant`, `CurrentFactory`, `CurrentPlant`, `CurrentWorkCenter` read
    from `InfrastructureContext`; `CurrentUser.HasPermission` honors the `resource.action` wildcard convention.
  - **Identifiers**: `IGuidGenerator`/`GuidGenerator` (random + version-7 sequential), `ICorrelationIdAccessor`/
    `CorrelationIdAccessor` (reads the ambient `IRequestContext`).
  - **Serialization**: `IJsonSerializer`/`JsonSerializer` over `System.Text.Json`, sharing the canonical `JsonOptions`.
  - **Caching**: `CacheKeyGenerator`, `MemoryCacheProvider` (over `IMemoryCache`), `CacheService` (typed JSON facade,
    default TTL from options, `ILogger<CacheService>` diagnostics).
  - **File storage**: `LocalFileProvider` (read) and `FileStorage` (read/write) over the local file system, with
    directory-traversal protection beneath the configured root.
  - **Localization**: `LocalizationProvider` implements `ILocalizationService` with a culture→key catalog and
    key-as-fallback semantics.
  - **Transactions**: `Transaction` + `TransactionManager` bound to the shared-kernel `IUnitOfWork` (commit flushes,
    rollback leaves the scoped unit of work unflushed).
  - **DI**: `AddInfrastructureFoundation(configuration)` registers all of the above (`TryAdd` throughout, options
    pattern, `AddMemoryCache`); wired into the existing `AddInfrastructure`. Added `Microsoft.Extensions.Caching.Memory`,
    `Options`, `Options.ConfigurationExtensions`, `Configuration.Binder`, `Logging.Abstractions`; a scoped `.editorconfig`
    disables CA1848/CA1873 (readability-first infrastructure logging).
- **Tests** — 52 unit tests in `FactoryOS.Tests/Infrastructure/` (DI registration + resolution; clock; GUID/correlation
  generators; cache key/provider/service; file storage round-trip + traversal rejection; localization fallback/format;
  transaction commit/rollback; current-context + wildcard permissions). `FactoryOS.Tests` references
  `FactoryOS.Infrastructure`.

Notes
- **Not duplicated**: `SystemClock` implements the Application `IApplicationClock` (Domain's `IDateTimeProvider` and
  `SystemDateTimeProvider` are left untouched); the transaction commits the shared-kernel `IUnitOfWork`; the wildcard
  matcher mirrors the convention each layer already matches independently (Gateway/Identity).
- Architecture impact: none to existing layers — only the Infrastructure project (plus the unit-test project) changed.
  Build green (0/0); .NET tests 860 → 912.

### Commit 0004 — Application foundation (2026-07-20)

Added
- **`FactoryOS.Application`** — the application-layer contracts and pipeline (the project was previously empty). All
  new; reconciled with the existing architecture (no duplicate abstractions, `.NET 10`):
  - **CQRS** (`Messaging`): `ICommand`, `ICommand<T>`, `IQuery<T>`, `IStreamQuery<T>` + their handlers;
    `IPipelineBehavior<TRequest,TResponse>`, `RequestHandlerDelegate<T>`, `IRequestContext`, `IAuthorizedRequest`.
  - **Validation**: `IValidator<T>`, `ICommandValidator<T>`, `IQueryValidator<T,R>`, `IValidationResult`,
    `ValidationResult`, `ValidationFailure`, `IValidationContext<T>`.
  - **Services**: `IApplicationClock` (extends the reused `IDateTimeProvider`), `ICurrentUser`, `ICurrentTenant`,
    `ICurrentFactory`, `ICurrentPlant`, `ICurrentWorkCenter`.
  - **Notifications**: `INotificationPublisher`, `IDomainEventPublisher`, `IIntegrationEventPublisher`.
  - **Caching**: `ICacheService`, `ICacheProvider`, `ICacheKeyGenerator`. **Transactions**: `ITransaction`,
    `ITransactionManager`. **Files**: `IFileStorage`, `IFileProvider`. **Localization**: `ILocalizationService`.
    **Mapping**: `IObjectMapper`.
  - **Behaviors**: `LoggingBehavior`, `ValidationBehavior`, `PerformanceBehavior`, `TransactionBehavior`,
    `AuthorizationBehavior` (open generics).
  - **Configuration**: `ApplicationContext` (scoped `IRequestContext`), `ApplicationOptions`, `ApplicationConstants`.
  - `AddApplication` now registers the options, scoped request context and the five behaviors in composition order.
    Added `Microsoft.Extensions.Logging.Abstractions`; a scoped `.editorconfig` disables CA1040/CA1848/CA1873 for this
    project (intentional CQRS marker interfaces + readability-first behavior logging).
- **Tests** — 14 unit tests in `FactoryOS.Tests/Application/` (registration wiring; validation/authorization/
  transaction/logging behaviors; `ValidationResult`; `ApplicationContext`). `FactoryOS.Tests` references
  `FactoryOS.Application`.

Notes
- **Not duplicated**: the clock (`IApplicationClock` extends Domain's `IDateTimeProvider` rather than shadowing it);
  integration events reuse `FactoryOS.Contracts.Events.IIntegrationEvent`.
- Architecture impact: none to existing layers — only the (empty) Application project and the unit-test project
  changed. Build green (0/0); .NET tests 846 → 860.

### Commit 0003 — Shared Kernel primitives (2026-07-20)

Added
- **`FactoryOS.Shared`** — the genuinely-missing, self-contained shared-kernel primitives (the project was previously
  an empty marker). Reconciled with the existing architecture: primitives already living in `FactoryOS.Domain`
  (`Result`, `Error`, `ValueObject`, `Entity`, `AggregateRoot`, `Specification`, `IRepository`, `IDateTimeProvider`)
  were **not** duplicated. Added:
  - **Value objects** (self-contained records): `Money`, `Percentage`, `DateRange`, `Period`, `EmailAddress`,
    `PhoneNumber`, `Address`, `GeoLocation`.
  - **Pagination**: `SortDirection`, `PageRequest`, `PaginationMetadata`, `PagedResult<T>` (+ factory).
  - **Primitives**: `Enumeration` (smart-enum base).
  - **Identifiers** (`readonly record struct`): `CorrelationId`, `TenantId`, `UserId`, `MachineId`, `FactoryId`,
    `OrganizationId`, `PlantId`, `LineId`, `WorkCenterId`, `LocalizationKey`, `ErrorCode`.
  - **Auditing**: `AuditInfo`, `IAuditable`, `ISoftDelete`, `IHasConcurrencyToken`.
  - **Exceptions**: `DomainException` base + `BusinessException`, `ValidationException`, `NotFoundException`,
    `ConflictException`, `UnauthorizedException`, `ForbiddenException`.
  - **Guards**: `Guard` (argument guards), `Ensure` (invariants), `Throw` (terminal helpers).
  - **Extensions**: `String`, `DateTime`, `Enumerable`, `Collection`, `Object`, `Json`.
  - **Constants**: `RegexPatterns` (source-generated matchers), `HeaderNames`, `ClaimTypeNames`, `RoleNames`,
    `PermissionNames`, `PolicyNames`, `ErrorCodes`, `CacheKeys`, `LocalizationKeys` — grounded in the real system.
  - **Abstractions**: `IUnitOfWork`.
- **Tests** — 39 unit tests in `FactoryOS.Tests/Shared/` (value-object equality & validation, Money arithmetic,
  DateRange, Guard, Pagination, Enumeration, strongly-typed IDs, extensions). `FactoryOS.Tests` gained a reference to
  `FactoryOS.Shared`.

Notes
- **Not duplicated** (already in `FactoryOS.Domain`, per "use the existing architecture exactly as it is"): `Result`,
  `Error`, `ValueObject`, `Entity`, `AggregateRoot`, `Specification`, `IRepository`, `IDateTimeProvider`.
- **Omitted deliberately**: empty marker interfaces `IEntity`/`IAggregateRoot`/`IValueObject`/`IDomainService`/
  `IApplicationService` and a parallel `IClock`/`IGuidGenerator`/`IDomainEvent` — they would either violate CA1040
  (empty interfaces) under the repo's analyzer policy or duplicate/shadow the Domain kernel. Kept on **.NET 10**.
- Build green (0 warnings / 0 errors under `latest-recommended` analyzers + warnings-as-errors); .NET tests 807 → 846.

### Commit 0001 — Repository foundation structure (2026-07-20)

Added
- Standard repository scaffolding folders, added **without overwriting or deleting** anything, each empty leaf kept in
  git with a `.gitkeep`:
  - Root: `build/`, `deploy/`, `scripts/`, `tools/`, `samples/`, `assets/` (`src/`, `tests/`, `docs/` already existed).
  - `docs/`: `api/`, `modules/`, `connectors/`, `ai/`, `iot/`, `security/`, `deployment/`, `testing/`, `releases/`
    (`architecture/` and `contracts/` already existed).
  - `build/`: `docker/`, `github/`, `pipelines/`, `templates/`.
  - `deploy/`: `docker/`, `kubernetes/`, `sql/`.
  - `scripts/`: `setup/`, `build/`, `test/`, `release/`.
  - `tools/`: `codegen/`, `analyzers/`, `migrations/`.

Notes
- Structure only — no solution, projects, source, NuGet references, Dockerfile, compose, CI or configuration were
  created, per scope. All 13 root files already exist and were left untouched. `.gitignore` gained frontend ignores
  (`node_modules/`, `dist/`, `web/.env`) so the first push excluded build artifacts.

### Sprint 1A — Solution foundation, reconciled onto the existing repo (2026-07-20)

Added
- Missing repository-foundation files, added **without overwriting** any existing global file and **without**
  downgrading the framework: the repo stays on **.NET 10** as the ratified Constitution mandates.
  - **`LICENSE`** — proprietary (all rights reserved) notice for FactoryOS Enterprise.
  - **`.gitattributes`** — line-ending normalization (LF in repo, CRLF for `.sln`/`.ps1`), binary markers, and
    `linguist-generated` on `bin`/`obj`/`web/dist`.
  - **`NuGet.Config`** — a single explicit `nuget.org` source with package source mapping, for reproducible restores.
  - **`Directory.Packages.props`** — Central Package Management anchor, present but **disabled**
    (`ManagePackageVersionsCentrally=false`) so the existing inline-versioned projects keep building; a documented
    migration flips it on later. Build verified: 0 warnings / 0 errors.
  - **`SECURITY.md`**, **`CODE_OF_CONDUCT.md`**, **`CONTRIBUTING.md`** — private vulnerability disclosure, Contributor
    Covenant 2.1, and a contribution guide grounded in the Constitution, the .NET 10 stack and the real build/test
    commands.

Notes
- **Not created (already present, left untouched):** `README.md`, `CHANGELOG.md`, `.editorconfig`, `.gitignore`,
  `Directory.Build.props`, `global.json`, `docker-compose.yml`, `Dockerfile`.
- **Deliberately not created:** a competing `FactoryOS.sln` (the repo's solution of record is `FactoryOS.slnx`) and the
  numbered `01-Core … 12-Build` physical folders (the Constitution's `src/Core`, `plugins/`, `connectors/`, `agents/`,
  `edge/`, `web/`, `tests/`, `docs/` layout already realizes those groupings; adding parallel empties would duplicate
  structure and violate the layout law). No projects, code or NuGet references were added, per the foundation scope.

### Sprint 117 — Quality line quarantine: the write-action pattern generalizes (2026-07-20)

Added
- **`plugins/quality`** — a second permission-guarded write action, proving the Sprint 113 `RequirePermission` primitive
  generalizes across modules: `POST /m/quality/lines/{lineId}/quarantine`, guarded by `quality.quarantine`. A new
  idempotent `IQuarantineStore` tracks held lines per tenant; on a real transition the module publishes the new
  `QualityLineQuarantined` event, and the read `/lines` rows now carry a `quarantined` flag so the dashboard reflects
  the hold. `QuarantinedBy` is taken from the request principal; an optional `reason` may accompany the request.
- **`plugins/activity`** + **`plugins/dashboard`** — both consume `QualityLineQuarantined` (fan-out, no reference to
  Quality): Activity records a "Quality" line naming who held it and why; the dashboard pushes a `Warning` tile. Both
  idempotent by event id. Added to each manifest's `consumes`; `quality` emits the new event.
- **`web/`** — the Quality screen gained a per-line **Quarantine** action (hidden unless the caller holds
  `quality.quarantine`, and for already-held lines) plus a `Quarantined` chip; `client.quarantineLine`.
- **`docs/contracts/permissions.sample.json`** — lists the `quality.quarantine` write action (the existing
  `QualityInspector` role already holds it via `quality.*`).

Tests
- Quality store unit: quarantine reports the transition then is idempotent, reads back per line, and never crosses
  tenants. Api integration (`QualityQuarantineApiTests`): quarantining with the permission (or a `quality.*` wildcard)
  returns `200` and publishes exactly one event; `quality.view` alone is `403`; a repeat is `200` with
  `newlyQuarantined:false` and announces once. Activity + Dashboard handler units. Web (`client.test.ts`):
  `quarantineLine` POSTs correctly and surfaces a `403` message. .NET 798 → 807, web 34 → 36.

Notes
- No Constitution impact: the write is the plugin's own, authorization is the shared gateway primitive, and two more
  decoupled consumers fan out from the one `QualityLineQuarantined` contract (Law 4).

### Sprint 116 — Dashboard consumes WorkOrderClosed as an informational alert (2026-07-20)

Added
- **`plugins/dashboard`** — a new `Info` alert level for positive/resolving signals, and a `WorkOrderClosedHandler`
  that folds `WorkOrderClosed` into the operations board as an `Info` tile ("Work order … closed on …") — a calm,
  positive counterpart to the warnings that raise work. Idempotent by event id and references only the shared event,
  never the Maintenance module (Law 4). `WorkOrderClosed` added to the Dashboard manifest's `consumes`. The board's
  existing `?level=` filter and `alertsByLevel` rollup now cover `Info` for free.
- **`web/`** — `levelTone` maps the new `Info` level to the positive `ok` badge tone, so a closed-work-order tile reads
  calmly (emerald) rather than as a neutral or alarming one.

Tests
- Dashboard handler unit: a closed work order folds into a single `Info` alert naming the order, and a redelivery
  (same event id) does not double it.
- Web (`format.test.ts`): `Info` now maps to `ok`, and an unknown level still falls back to `neutral`. .NET 797 → 798.

Notes
- No Constitution impact: two more decoupled consumers (Activity from Sprint 115, Dashboard now) fan out from the one
  `WorkOrderClosed` contract; the producer knows none of them, and each is independently removable.

### Sprint 115 — The close echoes on the bus: Activity consumes WorkOrderClosed (2026-07-20)

Added
- **`plugins/activity`** — a `WorkOrderClosedHandler` folds the new `WorkOrderClosed` event into the per-tenant
  activity feed as a "Maintenance" line naming who closed it (or "the system" when no actor is known). This closes the
  loop Sprint 113 opened: the permission-guarded close now echoes across the bus, and the operator's Activity Feed
  shows completion alongside the raising of work. The handler references only the shared event vocabulary, never the
  Maintenance module (Law 4), and recording is idempotent by event id, so at-least-once delivery never doubles the
  entry. `WorkOrderClosed` added to the Activity manifest's `consumes`.

Tests
- Activity handler units: a closed work order becomes a "Maintenance" entry naming the closer (`tech-1`); with no
  actor it reads "closed by the system".
- Api integration (`MaintenanceCloseToActivitySpineTests`): publishing `WorkOrderClosed` on the bus lands exactly one
  activity entry — proving the fan-out and idempotency (a redelivery does not double it) with no inter-module
  reference. .NET 794 → 797.

Notes
- No Constitution impact: pure event-driven fan-out — the producer (Maintenance) and consumer (Activity) share only
  the `WorkOrderClosed` contract, and either can be removed without touching the other or the core.

### Sprint 114 — Web: permission-aware screens (2026-07-20)

Added
- **`web/`** — screens now receive a wildcard-aware `holds(permission)` predicate, so the Work Orders screen hides the
  **Close** action (and its column) unless the caller holds `maintenance.close`. The predicate comes from `permitFor`
  over the caller's effective permissions — a signed session's own claims, else the dev `?perms=` set, else
  unrestricted — mirroring the gateway's own convention (`*`, `resource.*` boundary match, exact, case-insensitive).
  This is a UX aid, not a security boundary: the gateway still re-checks every write (a spoofed client can hide a
  button but never perform the action). `lib/permissions.ts` (`grants`, `permitFor`); `ScreenProps` gained `holds`,
  threaded through `App` → `OperatorShell` → screen.

Tests
- Web (`permissions.test.ts`): `grants` treats null as unrestricted, an empty set as denying, honors `*`, the
  `resource.*` boundary (grants `maintenance.close`, not `maintenancex.close`), and exact case-insensitive matches;
  `permitFor` closes over the held set. Web suite 28 → 34.

Notes
- No Constitution or backend impact: authorization stays server-side (Sprint 113's `RequirePermission`); this only
  aligns what the UI offers with what the caller can actually do.

### Sprint 113 — Work-order close: a permission-guarded write action (2026-07-20)

Added
- **`FactoryOS.Gateway`** — a `RequirePermission("<key>")` route filter, the write-side counterpart to navigation
  filtering: it authorizes an action at the API boundary and rejects a caller lacking the permission with
  `403 Forbidden`. Hiding a screen keeps an unentitled user from seeing an action; only this filter *authorizes* it, so
  a hand-crafted request cannot perform it. Consistent with additive RBAC, an unrestricted request is allowed.
- **`plugins/maintenance`** — the first write action: `POST /m/maintenance/workorders/{number}/close`, guarded by
  `maintenance.close`. The store gained an idempotent `Close` (`NotFound` / `AlreadyClosed` / `Closed`); on a real
  transition the module publishes the new `WorkOrderClosed` event on the bus — reporting/notification/dashboard react
  without referencing Maintenance — and closing an already-closed order changes and announces nothing. `ClosedBy` is
  taken from the request principal when present.
- **`web/`** — the Work Orders screen gained a per-row **Close** action (hidden for already-closed orders) that calls
  `closeWorkOrder`, refreshes the list, and surfaces a friendly message on a `403`. The real security boundary is the
  gateway, not the button.

Changed
- **`docs/contracts/permissions.sample.json`** — documents that write actions are authorized at the API boundary,
  lists the `maintenance.close` write action, and adds a `MaintenanceTechnician` example role (`maintenance.view` +
  `maintenance.close`).

Tests
- Maintenance store unit tests: `Close` transitions an open order, is idempotent on an already-closed one, reports
  `NotFound` for an unknown number, and never reaches across tenants.
- Api integration (`MaintenanceCloseApiTests`, TestServer with permission resolution + the event bus): closing with
  `maintenance.close` returns `200` and publishes exactly one `WorkOrderClosed`; closing with only `maintenance.view`
  is `403` and announces nothing; an unknown order is `404`; a repeat close is `200` but announces only once.
- Web (`client.test.ts`): `closeWorkOrder` POSTs to the right path with the tenant header and returns the closed
  order; a `403` surfaces a permission message. .NET 786 → 794, web 26 → 28.

Notes
- No Constitution impact: the gateway still knows only the permission-claim convention; the write is the plugin's own,
  authorization is a gateway primitive, and modules stay decoupled through the `WorkOrderClosed` event (Law 4).

### Sprint 112 — Web: login form + silent session renewal (2026-07-20)

Added
- **`web/`** — a real sign-in flow replaces the `?token=` URL hack. A `LoginPanel` collects credentials and exchanges
  them for a session; the header shows the signed-in user with a **Sign out** action, or a **Sign in** button when
  signed out. The session is owned by a `useSession` hook that persists per tenant (`localStorage`, scoped by tenant
  so switching factories never carries one identity into another), restores on reload, and **silently renews** the
  short-lived access token via `/auth/refresh` a minute before it expires — a failed renewal ends the session cleanly
  rather than leaving a dead token in play. The gateway client rebuilds when the token rotates, so a renewed token
  takes effect immediately; a signed session drives permissions from its claims, so the dev `?perms=` fallback only
  applies when signed out. `session.ts` (session model, per-tenant storage, `refreshDelayMs` timing), `useSession.ts`,
  `components/LoginPanel.tsx`.

Tests
- Web (`session.test.ts`): session storage round-trips per tenant and refuses another tenant's session or an unusable
  payload; `refreshDelayMs` schedules a skew ahead of expiry, clamps an expired token to an immediate refresh, and
  falls back to the skew for an unparseable expiry. Web suite 19 → 26.

Notes
- No new configuration: the session is entirely client-side over the existing `/auth/login` and `/auth/refresh`
  endpoints; token lifetimes come from the already-shipped `sample.identity.json` (`AccessTokenMinutes` /
  `RefreshTokenDays`). No Constitution impact — no core or gateway change.

### Sprint 111 — Refresh-token flow, end to end (2026-07-20)

Added
- **`FactoryOS.Api`** — a `POST /auth/refresh` endpoint rotates a still-active refresh token into a fresh
  access/refresh pair, so the SPA renews a short-lived access token without re-prompting for credentials. Rotation
  revokes the presented token, so a replayed or leaked older refresh token is rejected. Login now returns the
  refresh token and its expiry alongside the access token (`LoginResponse` gained `refreshToken` /
  `refreshTokenExpiresAt`). Both endpoints move into a shared `AuthEndpoints.MapAuthEndpoints()` extension so the
  host (`Program.cs`) and its integration tests wire the identical behaviour.
- **`web/`** — `refresh(tenant, refreshToken)` in the auth client posts to `/auth/refresh` and returns the rotated
  session; the `LoginResponse` type carries the refresh token and expiry the client stores to renew.

Tests
- Api integration (`AuthEndpointsTests`, TestServer over the real endpoints): a seeded user logs in and receives an
  access + refresh token; refreshing rotates the refresh token and preserves the `*` permissions; a rotated token
  cannot be replayed (401); an unknown token is rejected (401). Integration suite 188 → 192.
- Web (`auth.test.ts`): `login` and `refresh` post to the right path with the tenant header and body, return the
  session, and surface friendly messages on 401. Web suite 15 → 19.

Notes
- No Constitution impact: refresh is an Identity-layer capability the Api merely exposes; the gateway still knows only
  the `factoryos:permission` claim convention. `sample.identity.json` documents `AccessTokenMinutes` /
  `RefreshTokenDays` and the rotate-on-use semantics.

### Sprint 110 — Identity: default role/permission seed (2026-07-20)

Added
- **`FactoryOS.Identity`** — a `DefaultIdentitySeeder` seeds the default roles and one demo user per role so a fresh
  deployment has something to log in with, closing the `/auth/login → token → RBAC` loop end to end. Roles are data using
  the same wildcard permission surface the manifests declare: **Administrator** (`*`), **PlantSupervisor** (every
  `*.view`), **EnergyOperator** (`energy.*`, `dashboard.view`, `activity.view`), **QualityInspector** (`quality.*`, …) —
  users `admin` / `supervisor` / `energy` / `quality` under a fixed seed tenant. No credential is hard-coded: the demo
  password comes from configuration (a `${secret:…}` placeholder) and, when absent, roles are seeded but no users are
  invented. Gated by `Identity:Seed:Enabled` (off by default) and wired at start-up in the Api. `IdentitySeedOptions` /
  `IdentitySeedResult`; registered in `AddIdentityModule`. Sample in
  [src/FactoryOS.Api/sample.identity.json](src/FactoryOS.Api/sample.identity.json); 4 seeder tests proving a seeded user
  authenticates and its token carries exactly its role's permissions (admin `*`, energy `energy.*` but not `quality.view`).

### Sprint 109 — Security: meaningful screen permissions + wildcard matching (2026-07-20)

Added
- **gateway** — permission checks are now wildcard-aware, honoring the same `resource.action` convention the Identity
  layer issues: a held `*` grants everything, `resource.*` grants every action on a module, and an exact grant matches
  only itself (case-insensitive). `PermissionContext.Holds` keeps an O(1) exact fast-path, then falls back to
  `PermissionMatch.Grants`. So a role granting `energy.*` sees every Energy screen without listing each action.
- **plugins** — every first-party module screen now declares a `requiredPermission` in its `module.json`
  (`dashboard.view`, `energy.view`, `maintenance.view`, `quality.view`, `warehouse.view`, `oee.view`, `activity.view`,
  `brain.view`), so RBAC actually shapes the operator's nav: an unauthenticated request still sees everything
  (unrestricted), but a real session is narrowed to what its permissions grant. The permission surface and example roles
  (Administrator `*`, PlantSupervisor, EnergyOperator `energy.*`, QualityInspector `quality.*`) are documented in
  [docs/contracts/permissions.sample.json](docs/contracts/permissions.sample.json). 9 `PermissionMatch` cases + a gateway
  HTTP test (`energy.*` reveals Energy, hides an admin screen).

### Sprint 108 — Security: Identity-issued tokens drive RBAC (2026-07-20)

Added
- **gateway** — permission resolution now prefers an **authenticated principal**: `PermissionResolutionMiddleware` reads
  the permission claims off `HttpContext.User` (claim type `factoryos:permission`, configurable) when the request is
  authenticated, falling back to the `X-FactoryOS-Permissions` header only for tools and tests. The gateway consumes the
  claim-type convention the Identity layer issues — it still references no Identity types. New integration tests issue a
  **real Identity JWT** (`JwtAccessTokenService`), validate it into a principal, and prove navigation is filtered to the
  token's permissions (and that an invalid token leaves the request unrestricted).
- **Api** — a `BearerAuthenticationMiddleware` validates a `Bearer` access token with the Identity
  `IAccessTokenService` into the request principal (the Identity → gateway bridge, so the gateway stays Identity-free),
  and a `POST /auth/login` endpoint exchanges credentials via `IAuthenticator` for a signed token, returning it with its
  expiry and the effective permissions. Wired into the pipeline before permission resolution.
- **`web/`** — `GatewayClient` accepts an access token and sends it as `Authorization: Bearer` (which supersedes the dev
  permissions header); a new `login()` helper calls `/auth/login`; `App` picks up a session from `?token=`. So the shell
  filters its navigation by a real signed identity. +1 client test (15 Vitest tests, green build).

### Sprint 107 — Security: RBAC-filtered navigation (2026-07-20)

Added
- **gateway** — navigation is now filtered by the caller's permissions. A new request-scoped `IPermissionContext`
  (mirroring `ITenantContext`) is resolved at the edge by `PermissionResolutionMiddleware` from the
  `X-FactoryOS-Permissions` header; `/modules/ui/nav` and `/shell` run their nav through `NavigationPermissionFilter`,
  which drops any screen whose declared `requiredPermission` the caller doesn't hold and prunes sections left empty.
  RBAC is **additive**: with no permission header the context is *unrestricted* and everything shows (so every existing
  route is unaffected), and it narrows the surface only when a set is supplied — configuration, never a customer branch.
  `AddPermissionResolution()` / `UsePermissionResolution()`; 3 filter unit tests + 1 gateway HTTP test (header hides the
  Admin section).
- **`web/`** — `GatewayClient` takes an optional permission set and sends it on every request (omitted entirely when
  unrestricted); `App` resolves it from `?perms=a,b`, so one build shows an operator and an admin different nav from the
  same gateway. `PERMISSIONS_HEADER`; +1 client test (14 Vitest tests, green build).

### Sprint 106 — Energy: a read model, API and live screen (2026-07-20)

Added
- **`energy`** — the last operations module without a read surface gets one. A new `IEnergyReadModel` (in-memory,
  tenant-partitioned, per-meter latest reading + a bounded newest-first spike feed) is fed straight from the existing
  `MeterReadingReceived` handler — each reading updates the meter's latest value and rolling baseline, and a detected
  spike is pushed onto the feed, all under the handler's existing idempotency. A new `EnergyApi` serves
  `GET /m/energy/meters` (latest value vs baseline with a signed `deltaPercent`), `GET /m/energy/spikes` (recent, newest
  first) and `GET /m/energy/summary` — no consumer touches the Edge Gateway or a meter. Manifest gains the routes and the
  `energy.readmodel` capability; `sample.config.json` documents the shape and the new `spikeFeedCapacity` option. 2 handler
  unit tests (read model tracks latest + spike) and 3 gateway HTTP tests (meters with delta, spikes newest-first, tenant
  required).
- **`web/`** — the operator shell's **Energy** screen now resolves to a real component (`energy/Dashboard`): per-meter
  cards showing the latest value against baseline with a color-graded delta, plus a recent-spike list — so every nav item
  now resolves to a live screen. `GatewayClient.energyMeters`/`energySpikes`; registry test updated (13 Vitest tests, green
  build).

### Sprint 105 — Experience: four more operator screens go live (2026-07-20)

Added
- **`web/`** — the operator shell now renders four more module screens from their real read APIs, resolved through the
  `component`-id registry exactly as their manifests declare them: **Maintenance** work orders (`/m/maintenance/workorders`,
  status chips), **Quality** by line/product (`/m/quality/lines`, defect rate with breach flags), **Warehouse** stock
  (`/m/warehouse/stock`, below-reorder highlighting) and **OEE** per machine (`/m/oee/snapshots`, with the
  Availability × Performance × Quality breakdown). A module the shell wasn't built with (e.g. `energy/Dashboard`) still
  appears in the nav and falls back gracefully — proving screens are data. `GatewayClient` gains `workOrders`,
  `qualityLines`, `warehouseStock`, `oeeSnapshots`; +1 registry test asserting every first-party `component` id resolves
  and unknown ids return null (13 Vitest tests total, green build).

### Sprint 104 — Experience: the Company Brain becomes conversational (2026-07-20)

Added
- **`web/`** — the operator shell's Brain screen now **asks**: the form POSTs `/m/brain/ask` (202 Accepted; asking is
  decoupled from answering), shows an optimistic "thinking…" row while the grounded answer is produced asynchronously
  over the bus (RAG + LLM Gateway), then refetches `/m/brain/answers` so the answer lands in the list. Closes the
  Company-Brain loop end-to-end in the UI — a floor operator holds a conversation without the shell ever touching the AI
  layer. `GatewayClient.askBrain(question, askedBy)`; +1 client contract test (11 Vitest tests total, green build).

### Sprint 103 — Experience: per-tenant branding in the shell (2026-07-20)

Added
- **gateway** — `GET /shell` now carries the resolved tenant's `branding` (display name, primary color, logo URL), so a
  single build themes itself per factory (Law 6: branding is per-tenant, never a core branch). A new
  `ITenantBrandingProvider` (default `TenantBrandingProvider`) does a per-tenant lookup, falling back to neutral branding
  (the tenant key as display name) for an unknown tenant — the shell always gets something to render. Registered neutrally
  in `AddModuleGateway()`; a host overrides it with a provider seeded from tenant configuration, keeping the gateway free of
  customer data. `TenantBranding` record. 2 provider unit tests + 2 gateway HTTP tests (neutral default, seeded tenant).
- **`web/`** — the shell applies branding on bootstrap: it binds the tenant's primary color (and a derived soft tint) to
  the CSS variables Tailwind's `brand` palette reads, sets the browser theme-color, and shows the factory's name and logo
  in the header (with `FactoryOS · <tenant>` kept as the platform tag). `applyBranding` helper; green build, 10 Vitest tests.

### Sprint 102 — Marketplace: the Store write side (enable/disable) (2026-07-20)

Added
- **gateway** — the Store gains a write side (Phase 5): `POST /store/plugins/{key}/enable` and
  `POST /store/plugins/{key}/disable` toggle a plugin's activation with no tenant branching. `200` returns the plugin's
  refreshed Store entry with **recomputed dependency health** (disabling one plugin can flip another's `satisfied`); `404`
  when no plugin has the key; `409` when the plugin is `Failed` (fix and reload first). Disabling is idempotent. A new
  `IPluginAdmin` / `PluginAdmin` (in `FactoryOS.Plugin.Hosting`) owns the descriptor-state transition; registered via
  `AddPluginAdministration()` inside `AddModuleGateway()`. In the first-party modular monolith, enabling returns a plugin
  straight to `Started`; the out-of-process Store runtime will load and start the assembly on enable. Sample in
  [docs/contracts/store-admin.sample.json](docs/contracts/store-admin.sample.json); 5 `PluginAdmin` unit tests + 3 gateway
  HTTP tests (state flip, cross-plugin dependency health, 404).
- **`web/`** — the admin console's marketplace table gains per-plugin **Enable/Disable** actions (hidden for `Failed`
  plugins); toggling refetches `/system` and `/store/*` so every dependency badge and the "needs attention" count
  re-resolve live. `GatewayClient.setPluginEnabled`; +1 client contract test (10 Vitest tests total, green build).

### Sprint 101 — Experience: the PWA shell (`web/`) (2026-07-20)

Added
- **`web/`** — the first frontend: a React + TypeScript + Tailwind PWA scaffolded with Vite, a **thin, manifest-driven
  shell** that never references a plugin by name. On load it calls `GET /shell` once (tenant + nav + api catalog) and
  builds its sidebar from the section-grouped navigation; screens resolve through a `component`-id registry (the frontend
  counterpart of "screens are data, lazy-loaded by id"), so a module the shell has never heard of still appears in the nav.
  Two areas share one build: the **operator shell** (customer/floor — Operations board from `/m/dashboard/board`, Company
  Brain from `/m/brain/answers`, the factory timeline from `/m/activity/feed`) and the **admin console** (platform operator
  — `/system` rollup and the `/store` marketplace with per-plugin dependency health). The tenant travels in the
  `X-FactoryOS-Tenant` header via a small typed `GatewayClient`, chosen per-request by `?tenant=`, so one build serves any
  factory. Vite dev-proxies the gateway's discovery/module routes; `.env.sample` documents configuration. Green
  `npm run build` (tsc + vite) and 9 passing Vitest tests (format/nav helpers + client header & route contract).

### Sprint 100 — Capstone: the operating system presents itself (`GET /system`) (2026-07-20)

The finale. FactoryOS answers, in one call, "what is this factory running, and is it healthy?"

Added
- **gateway** — a new endpoint `GET /system` returns the platform status: product and version (read from assembly
  metadata), how many plugins are installed and active, how many need attention (an unmet dependency), the sorted union
  of capabilities the **active** plugins provide, and how many distinct event types flow across them (the union of every
  active plugin's `consumes` and `emits`). A capstone rollup of the whole gateway discovery surface built this phase
  (`/modules`, `/modules/ui/nav`, `/shell`, `/store/plugins`, `/store/summary`) — and a living proof of the Constitution:
  the identical core reports a different capability set and event vocabulary for every factory **solely** by which plugins
  are active, with no customer branching anywhere. `SystemStatus` record. Sample response in
  [docs/contracts/system-status.sample.json](docs/contracts/system-status.sample.json); new gateway HTTP test
  (`Presents_the_system_status_rolled_up_from_active_plugins`).

_Sprints 0–100 complete — Core → Integration → AI Platform → Business Modules → Experience → Marketplace._

### Sprint 99 — Marketplace: the Store health summary (2026-07-20)

Added
- **gateway** — a new endpoint `GET /store/summary` returns a marketplace health headline: the total plugins installed, a
  per-state tally (`byState`, ordered by count descending, ties by state name), and `withUnmetDependencies` — how many
  plugins have at least one unsatisfied dependency (the Store's "needs attention" count). Computed from the same data as
  `/store/plugins`, so a marketplace dashboard gets a one-call install-health strip. Completes the `summary` breakdown
  convention across the gateway's discovery surface, mirroring `activity` (byCategory), `brain` (byModel) and `dashboard`
  (byKind). `StoreSummary` / `StoreStateTally` records. Sample response in
  [docs/contracts/store-summary.sample.json](docs/contracts/store-summary.sample.json); new gateway HTTP test
  (`Summarizes_the_store_by_state_and_unmet_dependencies`).

### Sprint 98 — Marketplace: the Store plugin catalog (2026-07-20)

Added
- **gateway** — a new endpoint `GET /store/plugins` returns the marketplace view of everything installed on the host —
  every plugin (active, disabled or failed) with its package metadata (key, name, version, description, author, state,
  provided capabilities) and, for each declared dependency, whether a satisfying plugin is currently **active**
  (`satisfied`: an active plugin with that key at a version ≥ the minimum; disabled/failed plugins satisfy nothing). This
  is the read foundation of the Store (Phase 5), the counterpart to `/modules` that adds package metadata and dependency
  health, so a marketplace UI can show what's installed and whether each plugin's requirements are met — built purely from
  manifests, no core-side branching. `StoreCatalog` / `StorePlugin` / `StoreDependency` records. Sample response in
  [docs/contracts/store-catalog.sample.json](docs/contracts/store-catalog.sample.json); new gateway HTTP test
  (`Serves_the_store_catalog_with_dependency_satisfaction`).

### Sprint 97 — Experience: one-call shell bootstrap (2026-07-20)

Added
- **gateway** — a new endpoint `GET /shell` returns everything a shell needs on a cold start in one call: the resolved
  tenant, the cross-module navigation (`/modules/ui/nav`) and the API discovery catalog (`/modules/api`), saving a fresh
  PWA three separate round-trips on load. A pure composition of the existing providers — an unresolved tenant is not an
  error (the shell can prompt), and the payload reflects exactly the active plugins with no core-side branching. Sample
  response in [docs/contracts/shell-bootstrap.sample.json](docs/contracts/shell-bootstrap.sample.json); new gateway HTTP
  test (`Bootstraps_the_shell_with_tenant_nav_and_apis_in_one_call`).

### Sprint 96 — Experience: cross-module shell navigation (2026-07-20)

Added
- **gateway** — a new discovery endpoint `GET /modules/ui/nav` returns the shell's navigation regrouped **by section**
  across every active module, the cross-module counterpart to the module-centric `/modules/ui` registry. Sections are
  ordered by name (Ordinal; screens declaring no section group under an empty name that sorts first); within a section,
  items order by declared order, then title, then owning module, and each item carries the manifest key of the module
  that owns it (plus route, component, icon, `requiredPermission`, order) so a PWA sidebar renders section headings and
  lazy-loads screens directly — built purely from manifests, so the nav varies only by which plugins are active.
  `IModuleUiCatalogProvider` gains `GetNavigation()` returning `NavCatalog` / `NavSection` / `NavItem`. Sample response in
  [docs/contracts/nav-catalog.sample.json](docs/contracts/nav-catalog.sample.json); new provider unit tests and a gateway
  HTTP test (`Serves_the_navigation_grouped_by_section_across_modules`).

### Sprint 95 — Experience: drill the board alert feed down by kind (2026-07-20)

Added
- **`dashboard`** — `GET /m/dashboard/board` now accepts an optional `kind` filter (e.g. `?kind=EnergySpikeDetected`),
  composable with the existing `level` filter, so a wall board or PWA can drill the live alert feed to a single event
  kind — the natural follow-through from the `alertsByKind` breakdown added in Sprint 94 (see a count, then click into it).
  Both filters narrow only the returned feed; `criticalAlertCount` stays a whole-board headline. Manifest `query` list and
  `sample.config.json` document the new parameter; new integration test `Filters_the_alert_feed_by_kind_composably_with_level`.

### Sprint 94 — Experience: the board summary breaks alerts down by kind (2026-07-20)

Added
- **`dashboard`** — `GET /m/dashboard/summary` now also returns `alertsByKind`, a per-kind tally of the live alert feed
  ordered by count descending (ties broken by kind name, Ordinal), e.g.
  `alertsByKind: [ { kind: "EnergySpikeDetected", count: 2 }, { kind: "SafetyStandDownTriggered", count: 1 } ]`.
  Now that the feed carries six alert kinds (safety, quality, low-stock, energy, delivery), a wall board can show *what
  kind* of trouble dominates at a glance, not just a raw count — completing the `summary` breakdown convention shared
  with `activity` (byCategory) and `brain` (byModel). Computed purely from the board snapshot in the read API; no domain
  change. `sample.config.json` and the manifest route description document the new field; new integration test
  `Summary_breaks_the_feed_down_by_kind_count_descending`.

### Sprint 93 — Experience: the wall board surfaces delivery degradation (2026-07-20)

Added
- **`dashboard`** — the operations board now consumes `DeliveryHealthDegraded` and folds it into the live alert feed as a
  `Warning` tile (`"Delivery degraded on {transport}: {n} consecutive failures ({failed} of {attempts} attempts
  failed)"`), so a wall board sees an outbound-transport (webhook/etc.) degradation the moment Delivery Health raises it —
  the same alert the Activity Feed already timelines. Idempotent by event id; the Dashboard references the shared
  `DeliveryHealthDegraded` vocabulary, never the Delivery Health module or the connectors. Manifest `consumes` and
  `sample.config.json` updated; new unit test (`A_delivery_degradation_becomes_a_warning`) and integration spine test
  (`DeliveryToDashboardSpineTests`: `NotificationDelivered×N → DeliveryHealthDegraded → operations board`).

### Sprint 92 — Experience: the wall board surfaces energy spikes (2026-07-20)

Added
- **`dashboard`** — the operations board now consumes `EnergySpikeDetected` and folds it into the live alert feed as a
  `Warning` tile (`"Energy spike on {meter}: {metric} {value}{unit} is {delta}% over baseline {baseline}{unit}"`),
  joining the safety/quality/low-stock alerts it already surfaces. The wall board and PWA now see energy anomalies at a
  glance, closing the gap where a spike reached the Activity Feed (Sprint 80) and the Knowledge agent (Sprint 83) but
  not the Experience layer's board. Idempotent by event id; the Dashboard references the shared `EnergySpikeDetected`
  vocabulary, never the Energy module. Manifest `consumes` and `sample.config.json` updated; new unit test
  (`An_energy_spike_becomes_a_warning`) and integration spine test (`EnergyToDashboardSpineTests`:
  `MeterReadingReceived → EnergySpikeDetected → operations board`).

### Sprint 91 — Experience: an at-a-glance Brain answer summary (2026-07-20)

Added
- **`brain`** — a new read endpoint `GET /m/brain/summary` returns a tenant's answer-log headline: the total grounded
  answers kept and a per-model tally ordered by count descending (ties broken by model name), e.g.
  `{ total: 37, byModel: [ { model: "fast", count: 30 }, { model: "reasoning", count: 7 } ] }`. `IBrainAnswerLog`
  gains `Summarize(tenant)` returning `BrainAnswerLogSummary` / `BrainModelTally`; the in-memory log tallies under its
  per-tenant lock. Symmetric with the activity summary from Sprint 90 and the wider `summary` convention, giving a
  wall board a one-call view of Brain usage per model. The manifest advertises the route and `sample.config.json`
  documents the shape.

Notes
- Read-only and tenant-scoped by construction; an unknown tenant summarizes to zero. Proven by two new
  `InMemoryBrainAnswerLog` unit tests (count-descending model tally with name tie-break, unknown-tenant zero) and an
  over-the-gateway `BrainApiTests` case. Zero core changes.

### Sprint 90 — Experience: an at-a-glance activity summary (2026-07-20)

Added
- **`activity`** — a new read endpoint `GET /m/activity/summary` returns a tenant's feed headline: the total entries
  kept and a per-category tally ordered by count descending (ties broken by category name), e.g.
  `{ total: 42, byCategory: [ { category: "Production", count: 18 }, { category: "Insight", count: 9 }, ... ] }`.
  `IActivityFeed` gains `Summarize(tenant)` returning `ActivityFeedSummary` / `ActivityCategoryTally`; the in-memory
  feed tallies under its per-tenant lock. This mirrors the existing `summary` convention (`oee/summary`,
  `insight/summary`, `maintenance/summary`), giving a wall board or nav badge a one-call overview of the ten-category
  timeline without paging the feed. The manifest advertises the route and `sample.config.json` documents the shape.

Notes
- Read-only and tenant-scoped by construction; an unknown tenant summarizes to zero. Proven by three new
  `InMemoryActivityFeed` unit tests (count-descending tally with name tie-break, tenant isolation, unknown-tenant
  zero) and an over-the-gateway `ActivityApiTests` case. Zero core changes.

### Sprint 89 — AI: the Company Brain remembers its own insights (2026-07-20)

Added
- **`agent.knowledge`** — a new `InsightGeneratedHandler` narrates the Insight agent's `InsightGenerated` into a
  knowledge document and ingests it through the Knowledge Indexer under source ids `activity/insight/*`, so the
  Company Brain remembers its own past root-cause hypotheses and can ground later answers on them (*"has this failure
  been explained before?"*). The agent references only the shared event vocabulary, never the Insight agent, and
  reaches embeddings only over the HTTP indexer/embedding gateway — never an in-process model. Idempotent by
  construction: the source id derives from the event id, so re-ingest upserts. The `agent.json` `consumes` list and
  `sample.config.json` sources note are updated.

Notes
- Closes the loop opened in Sprint 88: an AI insight now lands on **both** the human timeline (Activity Feed) and the
  Brain's own memory (Knowledge agent) — the AI layer's interpretations become durable, retrievable context for
  future questions, a genuine organizational memory of *why*, not just *what*. Both noteworthy sinks now cover ten
  event kinds. Proven by a new `KnowledgeIngestTests` case asserting an `activity/insight/*` source and the
  subject/insight in the narrated text.

### Sprint 88 — Experience: AI insights land on the activity feed (2026-07-20)

Added
- **`activity`** — a new `InsightGeneratedHandler` folds the Insight agent's `InsightGenerated` into the timeline
  under a tenth category, `Insight`, e.g. *"AI insight on plant-1 (SafetyStandDownTriggered): Repeated near-misses on
  line 3 suggest a guarding gap; inspect before restart."* The plugin references only the shared event vocabulary,
  never the Insight agent — the bus fans the AI output out to whoever cares, so an operator sees the root-cause
  hypothesis on the human timeline alongside the raw alert that triggered it. Idempotent by construction: the entry
  is keyed by the insight's own event id, so at-least-once redelivery is a no-op. The manifest `consumes` list and
  `sample.config.json` categories note are updated.

Notes
- The Activity Feed now surfaces not just what happened (alerts, milestones) but the AI layer's *interpretation* of
  it — the first category sourced from an agent rather than a business module. Proven by a new `ActivityHandlersTests`
  unit case and an over-the-bus `InsightToActivitySpineTests` spine (`InsightGenerated → activity feed`, asserting a
  single "Insight" entry survives redelivery). Read-only; the feed is still fed exclusively by integration events on
  the bus, with zero core changes.

### Sprint 87 — AI/Experience: ask the Company Brain over HTTP (2026-07-20)

Added
- **`brain`** — the *ask* half of the Company Brain's HTTP surface: `POST /m/brain/ask` accepts a
  `{ question, askedBy? }` body, publishes a `BrainQuestionAsked` on the bus and returns `202 Accepted` with the
  question id (which correlates to the eventual answer's source id). Asking is decoupled from answering — the
  endpoint speaks only the shared event vocabulary and never touches the RAG/LLM stack. Empty questions are rejected
  `400`; a missing tenant is rejected `400` by the gateway. The manifest now advertises the endpoint and
  `emits: [ BrainQuestionAsked ]`; `sample.config.json` documents the request/response shape.

Notes
- Closes the Company Brain's HTTP loop end to end: **HTTP ask → bus → Brain Query agent (`agent.brain`, RAG + LLM
  Gateway over HTTP) → `BrainAnswered` → answer log (Sprint 86) → HTTP read**. `BrainQuestionAsked` finally has a
  first-party producer (the ingress noted as missing in prior analysis). Proven by a `BrainAskApiTests` gateway suite:
  ask publishes the question (trimmed, with `askedBy`, id echoed), empty question → 400, missing tenant → 400, and a
  full-loop test where a stand-in agent answers on the bus and the answer surfaces at `GET /answers`. Timestamps use
  the system clock in the endpoint; the plugin's dependency surface stays Contracts + Gateway only, like every other
  read-model plugin.

### Sprint 86 — AI/Experience: the Company Brain gets an HTTP answer log (2026-07-20)

Added
- **`brain`** (new plugin) — the HTTP face of the Company Brain. A new `BrainAnsweredHandler` consumes the shared
  `BrainAnswered` the Brain Query agent (`agent.brain`) re-enters on the bus and folds each grounded answer into a
  per-tenant, newest-first `IBrainAnswerLog` (in-memory, bounded, idempotent by the answer's source event id). A read
  API mounts at `/m/brain/answers` (via the manifest key, zero core changes) and serves each answer with its
  question, model and knowledge citations, so a UI can show conversational Q&A history **without ever touching the AI
  layer** — it references only the shared event vocabulary, never the agent or the RAG/LLM stack. Ships with
  `module.json`, `sample.config.json`, the project wired into `FactoryOS.slnx` and both test projects.

Notes
- This is the *read* half of the Company Brain's HTTP surface; the *ask* half (`POST /m/brain/ask` publishing
  `BrainQuestionAsked`) follows next sprint, closing the loop HTTP ask → bus → agent → `BrainAnswered` → this log →
  HTTP read. Proven by six `InMemoryBrainAnswerLog` unit tests, two `BrainAnsweredHandler` unit tests, a
  `BrainApiTests` gateway suite (newest-first + max, requires-tenant 400, tenant isolation) and a
  `BrainAnsweredToLogSpineTests` over-the-bus spine (`BrainAnswered → answer log`, single entry survives redelivery).

### Sprint 85 — AI: the Company Brain remembers certification gaps (2026-07-20)

Added
- **`agent.knowledge`** — a new `CertificationGapDetectedHandler` narrates the HR module's `CertificationGapDetected`
  into a knowledge document and ingests it through the Knowledge Indexer under source ids `activity/compliance/*`,
  giving the Company Brain a retrievable compliance memory (*"which workers were staffed without a valid
  certification, and on which shifts?"*). The agent references only the shared event vocabulary, never the HR module,
  and reaches embeddings only over the HTTP indexer/embedding gateway — never an in-process model. Idempotent by
  construction: the source id derives from the event id, so re-ingest upserts. The `agent.json` `consumes` list and
  `sample.config.json` sources note are updated.

Notes
- Fully re-closes the two-sink alignment: every noteworthy alert the modules raise — rules, work orders, safety
  stand-downs, quality alerts, completed production orders, degraded delivery, energy spikes, low-stock crossings and
  certification gaps — now lands on both the human timeline (Activity Feed) and the Brain's memory (Knowledge agent).
  Proven by a new `KnowledgeIngestTests` case asserting an `activity/compliance/*` source and the worker/certification
  in the narrated text.

### Sprint 84 — AI: the Company Brain remembers low-stock crossings (2026-07-20)

Added
- **`agent.knowledge`** — a new `LowStockDetectedHandler` narrates the Warehouse module's `LowStockDetected` into a
  knowledge document and ingests it through the Knowledge Indexer under source ids `activity/warehouse/*`, giving the
  Company Brain a retrievable memory of stockouts (*"which items dropped below their reorder point this month?"*).
  The agent references only the shared event vocabulary, never the Warehouse module, and reaches embeddings only over
  the HTTP indexer/embedding gateway — never an in-process model. Idempotent by construction: the source id derives
  from the event id, so re-ingest upserts. The `agent.json` `consumes` list and `sample.config.json` sources note are
  updated.

Notes
- Continues realigning the two noteworthy sinks: the human timeline (Activity Feed) and the Brain's memory (Knowledge
  agent) both now remember energy spikes and low-stock crossings. Certification-gap narration to the Brain remains to
  fully re-close the gap. Proven by a new `KnowledgeIngestTests` case asserting an `activity/warehouse/*` source and
  the SKU/warehouse in the narrated text.

### Sprint 83 — AI: the Company Brain remembers energy spikes (2026-07-20)

Added
- **`agent.knowledge`** — a new `EnergySpikeDetectedHandler` narrates the Energy module's `EnergySpikeDetected` into
  a knowledge document and ingests it through the Knowledge Indexer under source ids `activity/energy/*`, giving the
  Company Brain a retrievable memory of energy anomalies (*"which meters spiked last week and by how much?"*). The
  agent references only the shared event vocabulary, never the Energy module, and reaches embeddings only over the
  HTTP indexer/embedding gateway — never an in-process model. Idempotent by construction: the source id derives from
  the event id, so chunk ids are stable and re-ingest upserts. The `agent.json` `consumes` list and
  `sample.config.json` sources note are updated.

Notes
- Realigns the two noteworthy sinks after the Sprint 80–82 activity-feed additions: the human timeline (Activity
  Feed) and the Brain's memory (Knowledge agent) both now remember energy spikes. Low-stock and certification-gap
  narration to the Brain remain to fully re-close the gap. Proven by a new `KnowledgeIngestTests` case asserting an
  `activity/energy/*` source and the meter/metric in the narrated text.

### Sprint 82 — Experience: certification gaps land on the activity feed (2026-07-20)

Added
- **`activity`** — a new `CertificationGapDetectedHandler` folds the HR module's `CertificationGapDetected` alert
  into the timeline under a ninth category, `Compliance`, e.g. *"Certification gap on shift shift-night-1: W-3391
  staffed without ForkliftLicense (EXPIRED)"*. The plugin references only the shared event vocabulary, never the HR
  module — the bus fans the alert out to whoever cares (Notification pages a supervisor from the same event; this
  feed keeps the human-readable line). Idempotent by construction: the entry is keyed by the alert's event id, so
  at-least-once redelivery is a no-op. The manifest `consumes` list and `sample.config.json` categories note are
  updated.

Notes
- This closes the last of the three previously-uncaptured noteworthy alerts (energy spikes in Sprint 80, low-stock
  in Sprint 81, certification gaps here) — every plant-floor alarm the modules raise now also lands on the human
  timeline. Proven by a new `ActivityHandlersTests` unit case and an over-the-bus `HrToActivitySpineTests` spine
  (`ShiftStaffed → CertificationGapDetected → activity feed`, asserting a single "Compliance" entry survives a
  redelivered staffing). Read-only; the feed is still fed exclusively by integration events on the bus, with zero
  core changes.

### Sprint 81 — Experience: low-stock crossings land on the activity feed (2026-07-20)

Added
- **`activity`** — a new `LowStockDetectedHandler` folds the Warehouse module's `LowStockDetected` alert into the
  timeline under an eighth category, `Warehouse`, e.g. *"Low stock on BOLT-M8 in wh-main: 12 on hand at or below
  reorder point 20"*. The plugin references only the shared event vocabulary, never the Warehouse module — the bus
  fans the alert out to whoever cares (Procurement already raises a purchase requisition from the same event; this
  feed keeps the human-readable line). Idempotent by construction: the entry is keyed by the alert's event id, so
  at-least-once redelivery is a no-op. The manifest `consumes` list and `sample.config.json` categories note are
  updated.

Notes
- The second of the three previously-uncaptured noteworthy alerts (energy done in Sprint 80; certification-gap
  remains). Proven by a new `ActivityHandlersTests` unit case and an over-the-bus `WarehouseToActivitySpineTests`
  spine (`StockMovementRecorded → LowStockDetected → activity feed`, asserting a single "Warehouse" entry survives a
  redelivered crossing movement). Read-only; the feed is still fed exclusively by integration events on the bus,
  with zero core changes.

### Sprint 80 — Experience: energy spikes land on the activity feed (2026-07-20)

Added
- **`activity`** — a new `EnergySpikeDetectedHandler` folds the Energy module's `EnergySpikeDetected` alert into the
  timeline under a seventh category, `Energy`, e.g. *"Energy spike on main-incomer: ActivePower at 200kW is 100%
  above baseline 100kW"*. The plugin references only the shared event vocabulary, never the Energy module — the bus
  fans the alert out to whoever cares (Maintenance already raises a corrective work order from the same event; this
  feed keeps the human-readable line). Idempotent by construction: the entry is keyed by the spike's event id, so
  at-least-once redelivery is a no-op. The manifest `consumes` list and `sample.config.json` categories note are
  updated.

Notes
- This captures the first of the three previously-uncaptured noteworthy alerts (energy spikes; low-stock and
  certification-gap remain). Proven by a new `ActivityHandlersTests` unit case and an over-the-bus
  `EnergyToActivitySpineTests` spine (`MeterReadingReceived → EnergySpikeDetected → activity feed`, asserting a
  single "Energy" entry survives a redelivered spiking reading). Read-only; the feed is still fed exclusively by
  integration events on the bus, with zero core changes.

### Sprint 79 — Experience: the activity feed is filterable by category (2026-07-20)

Added
- **`activity`** — the read API `GET /m/activity/feed` now accepts an optional `category` query parameter
  (case-insensitive), so a UI or operator can narrow the timeline to one bucket, e.g.
  `GET /m/activity/feed?tenant=acme&category=Production&max=50`. `IActivityFeed.Recent` gains an optional
  `category` argument; the in-memory feed walks newest-first and takes up to `max` matching entries, so paging by
  category still respects the limit. A null/whitespace category returns every category (unchanged default). The
  manifest advertises the new `category` query and `sample.config.json` documents it.

Notes
- The three prior sprints enriched the feed to six categories (Rule, Maintenance, Safety, Quality, Delivery,
  Production); this makes that richer feed navigable rather than an undifferentiated stream. Purely additive — the
  existing two-argument `Recent` call sites are unaffected (the new parameter defaults to null). Proven by four new
  `InMemoryActivityFeed` unit tests (matching-only newest-first, case-insensitive, null/whitespace returns all,
  cap-at-max) and an over-the-gateway `ActivityApiTests` case (`?category=production` returns only the production
  line). Read-only; the feed is still fed exclusively by integration events on the bus.

### Sprint 78 — AI: the Company Brain remembers degraded notification delivery (2026-07-20)

Added
- **`agent.knowledge`** — a new handler, `DeliveryHealthDegradedHandler`, narrates the shared `DeliveryHealthDegraded`
  fact into a knowledge document (source id `activity/delivery/<id>`, carrying the transport, the consecutive-failure
  streak, the attempt/failure tallies and the last connector detail) and ingests it through the Knowledge Indexer.
  The Company Brain can now retrieve and cite delivery outages when asked ("why didn't alerts go out on the webhook
  channel last night?"). The manifest `consumes` `DeliveryHealthDegraded` (description updated) and
  `sample.config.json` documents the new `activity/delivery/*` source family.

Notes
- Completes the alignment begun in Sprints 76–77: the Activity Feed (human timeline) and the Knowledge agent (Brain
  memory) now cover the same noteworthy vocabulary — rules, work orders, safety, quality, completed production
  orders and degraded delivery. A degraded transport is exactly the kind of operational fact an operator later asks
  the Brain about, so the memory should hold it. Idempotent by construction (source id derives from the event id;
  the indexer upserts). Proven by a new `DeliveryHealthDegradedHandler` unit test (delivery source, transport and
  last-detail narrated, tenant scope). References the shared event only, never the Delivery Health module or connectors.

### Sprint 77 — AI: the Company Brain remembers completed production orders (2026-07-20)

Added
- **`agent.knowledge`** — a new handler, `ProductionOrderCompletedHandler`, narrates the shared
  `ProductionOrderCompleted` fact into a knowledge document (source id `activity/production/<id>`) and ingests it
  through the Knowledge Indexer (chunk → embed → store, over the embedding gateway — HTTP, never in-process). The
  Company Brain can now retrieve and cite finished orders when asked ("did order PO-42 complete, and how many
  units?"). The manifest `consumes` `ProductionOrderCompleted` (description updated), and `sample.config.json`
  documents the ingested source families.

Notes
- Closes a drift the previous sprint exposed: the Activity Feed and the Knowledge agent are the two parallel
  "noteworthy event" sinks — one a human timeline, one the Brain's RAG memory. Sprint 76 taught the feed about
  completed production orders; the Brain's memory had not kept pace, so it could not answer questions about them.
  This realigns the two. Idempotency is by construction — the source id derives from the producing event's id, so
  the indexer upserts stable chunk ids and a redelivery overwrites rather than duplicates. Proven by a new
  `ProductionOrderCompletedHandler` unit test (production source, narrated units, tenant scope). References the
  shared event only, never the Production module.

### Sprint 76 — Experience: completed production orders land on the activity feed (2026-07-20)

Added
- **`activity`** — a new consumer, `ProductionOrderCompletedHandler`, folds the shared `ProductionOrderCompleted`
  fact into a per-tenant, newest-first "Production" activity line (for example _"Production order PO-42 completed:
  widget — 11/10 units"_). It is the feed's first *milestone-reached* entry, sitting alongside its alert-shaped
  lines (fired rules, work orders, safety stand-downs, quality alerts, delivery-health). Recording stays idempotent
  by the producing event's id, so at-least-once redelivery never doubles the entry.
- **`activity`** — the manifest now `consumes` `ProductionOrderCompleted` (description updated), and
  `sample.config.json` documents the `Production` category.

Notes
- Closes the last dangling emit: `ProductionOrderCompleted` was published by the Production module but had no
  consumer. The Activity Feed now gives it a destination without referencing Production — the bus fans the fact out.
  Proven by a `ProductionOrderCompletedHandler` unit test and an over-the-bus `ProductionToActivitySpineTests`
  (order released → counts accrue → completing increment crosses target → single `Production` feed line, and a
  redelivery of the completing count neither double-counts the order nor doubles the entry):
  `ProductionOrderReleased + ProductionCountReported → ProductionOrderCompleted → activity feed`. With this every
  event a first-party manifest emits now has at least one consumer.

### Sprint 75 — Platform: the Rule Engine gains a third signal — carbon emissions fire rules (2026-07-20)

Added
- **`ruleengine`** — a new consumer, `CarbonEmissionCalculatedHandler`, evaluates the same declarative rules against
  computed `CarbonEmissionCalculated` facts, treating the per-reading emission as the Standard Model metric
  `CarbonCo2e` (kg CO₂e) and the emitting source as the "meter". A rule such as `CarbonCo2e GreaterThan 50 →
  RaiseSustainabilityAlert` thus fires on a high-emission reading exactly as a temperature rule fires on an over-temp
  reading — one rule vocabulary now over three signal streams (readings, OEE, carbon). Emits `RuleTriggered` once per
  (rule, emission event) pair via the pure `RuleEvaluator`.
- **`ruleengine`** — the manifest now `consumes` `CarbonEmissionCalculated`, and `sample.config.json` adds a
  `carbon-spike` rule (`CarbonCo2e GreaterThan 50 → RaiseSustainabilityAlert`).

Notes
- Gives `CarbonEmissionCalculated` a first consumer and extends automation to sustainability with zero new machinery:
  the emission stream reuses the same rules, evaluator and idempotent firing log as the other two. Because Sprint 71/72
  already route and explain any `RuleTriggered`, a carbon rule reaches the notification fabric and earns an AI insight
  for free: `CarbonEmissionCalculated → Rule Engine → RuleTriggered → Workflow/Insight`. Proven by
  `CarbonEmissionCalculatedHandlerTests` (source-as-meter, below-threshold silent, non-carbon rule ignored,
  case-insensitive metric, redelivery-once) and an over-the-bus case in `RuleEngineTriggerTests`. Existing signals
  unaffected; references shared events and the pure evaluator only, never the Carbon module.

### Sprint 74 — Automation: raised requisitions reach a destination — Workflow routes `PurchaseRequisitionRaised` (2026-07-20)

Added
- **`workflow`** — a new consumer, `PurchaseRequisitionRaisedHandler`, normalizes a raised purchase requisition
  (`PurchaseRequisitionRaised`) into a `WorkflowSignal` and runs it through the same engine, so the tenant's
  declarative rule for the `PurchaseRequisitionRaised` trigger routes it to a channel (for example a buyer's desk).
  The requisition's specifics (number, SKU, quantity, warehouse and the reason it was raised) travel in the subject.
  Requested once per triggering event.
- **`workflow`** — the manifest now `consumes` `PurchaseRequisitionRaised`, and `sample.config.json` adds a
  `PurchaseRequisitionRaised → Notify / procurement` rule.

Notes
- Closes the last standalone dangling-emit in the procurement loop: before this, `PurchaseRequisitionRaised` had no
  consumer — a raised requisition went nowhere. Now the full replenishment spine reaches the notification fabric with
  zero coupling: `StockMovementRecorded → Warehouse → LowStockDetected → Procurement → PurchaseRequisitionRaised →
  Workflow → WorkflowActionRequested` — proven end to end by `ProcurementToWorkflowSpineTests` over the real bus,
  plus unit tests for the handler (mapped-action + subject, no-rule, redelivery-once). Existing spines unaffected: a
  tenant with no `PurchaseRequisitionRaised` workflow rule routes nothing new.

### Sprint 73 — AI: generated insights become a read model — `/m/insight/*` (2026-07-20)

Added
- **`agent.insight`** — a new consumer, `InsightGeneratedHandler`, folds every emitted `InsightGenerated` into a
  per-tenant, bounded, newest-first **insight feed** (`IInsightFeed` / `InMemoryInsightFeed`, cap 200, dedup by event
  id, tenants isolated). The agent reads its own output back off the bus — never through an in-process call into the
  reasoning path — so the projection is fully decoupled from generation.
- **`agent.insight`** — a read API, `InsightApi : IModuleApi`, the gateway mounts under `/m/insight/*` from the
  manifest key: `GET /m/insight/feed` (query `tenant`, `max`) returns the tenant's recent insights newest-first, and
  `GET /m/insight/summary` returns the total kept plus a per-trigger-type tally. Tenant comes from the ambient
  `ITenantContext` (`X-FactoryOS-Tenant` header) and each endpoint `RequireTenant()`s.
- **`agent.insight`** — the manifest declares the two routes in its `api` block (so `/modules/api` discovery surfaces
  them, backing the already-declared `AI Insights` UI at `/insights`), now also `consumes` `InsightGenerated`, and
  `sample.config.json` documents the feed. The csproj gains the AspNetCore framework reference and a Gateway project
  reference.

Notes
- Closes the dangling-emit gap: before this, `InsightGenerated` was emitted but had no consumer — AI output never
  reached a read model or a screen. Now the agent both produces and serves its reasoning, mirroring OEE (emit +
  read API). Proven by `InsightApiTests` over the real gateway (newest-first, `max` cap, per-trigger summary, tenant
  required, tenant isolation) plus `InMemoryInsightFeedTests` for the read model (ordering, idempotency, bounded
  eviction, tally, isolation). AI stays out-of-process; the feed is a plain event projection, zero coupling.

### Sprint 72 — AI: the Insight agent explains fired rules — `RuleTriggered` earns a hypothesis (2026-07-20)

Added
- **`agent.insight`** — a new consumer, `RuleTriggeredHandler`, normalizes a fired Rule Engine automation
  (`RuleTriggered`) into the agent's uniform `InsightSignal` and runs its single reasoning path, so an automated
  threshold breach earns the same LLM-Gateway root-cause hypothesis and recommended action a human-facing alert
  would — re-entered onto the bus as `InsightGenerated`. The fired rule's specifics (id, metric, meter, observed
  value, operator, threshold and requested action) travel in the subject. Generated once per triggering event; a
  gateway failure throws so the bus retries.
- **`agent.insight`** — the manifest now `consumes` `RuleTriggered`, and `sample.config.json` documents the third
  trigger and broadens the system prompt to cover fired rules.

Notes
- Closes the observability-to-explanation gap: before this, the Insight agent reacted only to `SafetyStandDownTriggered`
  and `QualityAlertRaised` — the automation stream (including Sprint 70's OEE-degradation rule) fired but got no AI
  reasoning. Now every fired rule reaches the digital worker. AI stays out-of-process by construction: the agent
  touches a model only through the `ILlmGateway` (HTTP), never in-process, and references neither the Rule Engine nor
  any module. Proven end to end by `RuleToInsightChainTests` (`OeeCalculated → RuleTriggered → InsightGenerated`) over
  the real bus with a stubbed gateway, plus unit tests for the handler (subject naming the rule/metric/meter, prompt
  carries the details, redelivery-once). Existing insight triggers unaffected.

### Sprint 71 — Automation: fired rules reach a destination — Workflow routes `RuleTriggered` (2026-07-20)

Added
- **`workflow`** — a new consumer, `RuleTriggeredHandler`, normalizes a fired Rule Engine automation
  (`RuleTriggered`) into a `WorkflowSignal` and runs it through the same engine, so the tenant's declarative rule for
  the `RuleTriggered` trigger routes it to a notification channel. The fired rule's specifics (id, metric, observed
  value, operator and its own requested action) travel in the subject. Requested once per triggering event.
- **`workflow`** — the manifest now `consumes` `RuleTriggered`, and `sample.config.json` adds a `RuleTriggered →
  Notify / ops` rule.

Notes
- Closes the dangling-action gap: before this, a `RuleTriggered` whose action was, say, `NotifyEnergyDesk` had no
  consumer — only `RaiseMaintenanceAlert` (via Maintenance → work order) went anywhere. Now **any** fired rule reaches
  the notification fabric through the declarative Workflow layer, giving Sprint 70's OEE-degradation rule (and every
  metric/OEE rule) a destination. Full spine, zero coupling: `OeeCalculated → Rule Engine → RuleTriggered → Workflow
  → WorkflowActionRequested → Notification` — proven end to end by `OeeToNotificationSpineTests` over the real bus,
  plus unit tests for the handler (mapped-action + subject, no-rule, redelivery-once). Existing spines unaffected: a
  tenant with no `RuleTriggered` workflow rule routes nothing new.

### Sprint 70 — Platform: the Rule Engine gains a second signal — OEE degradation fires rules (2026-07-20)

Added
- **`ruleengine`** — a second consumer, `OeeCalculatedHandler`, evaluates the tenant's same declarative rules
  against computed `OeeCalculated` facts, treating OEE as the Standard Model metric `Oee` (a fraction in `[0, 1]`)
  and the machine as the "meter". A rule such as `Oee LessThan 0.6 → RaiseMaintenanceAlert` now fires on OEE
  degradation exactly as a temperature rule fires on an over-temp reading — one rule vocabulary over two signal
  streams. It emits `RuleTriggered` once per (rule, OEE event) pair; a redelivered event re-fires nothing.
- **`ruleengine`** — the manifest now `consumes` `OeeCalculated` alongside `MeterReadingReceived`, and
  `sample.config.json` adds an `oee-degraded` example rule documenting the `Oee` metric convention.

Notes
- Closes a real automation loop with zero coupling: OEE computes and emits `OeeCalculated`; the Rule Engine turns a
  degradation into a normalized `RuleTriggered`; Maintenance (which already consumes `RuleTriggered`) raises the work
  order — three plugins, no direct references, the whole turn on the bus. The pure `RuleEvaluator` is reused
  unchanged, so both signal streams share one exhaustively-tested decision (Law 4, event-driven end to end).
- Covered by unit tests (`OeeCalculatedHandlerTests`: degraded fires, healthy is silent, non-OEE rules never fire on
  OEE events, case-insensitive metric, redelivery-once) and an over-the-bus integration test.

### Sprint 69 — Experience: read APIs become discoverable data (`/modules/api` advertises the feeds) (2026-07-20)

Added
- **`oee`, `maintenance`, `warehouse`, `quality`, `dashboard`** — each manifest (`module.json`) now declares its HTTP
  read routes in an `api` block (method, path, query, description), matching the endpoints the plugin's `IModuleApi`
  actually mounts. The gateway's existing `/modules/api` discovery catalog aggregates these from the manifests, so a
  PWA, wall screen or AI agent finds every module's feeds **as data, never by referencing a plugin by name** — the
  five Experience feeds were being served but were invisible to discovery until now.
- **conformance** — `PluginManifestApiConformanceTests` reads the real `plugins/*/module.json` on disk and guards the
  invariant repo-wide: every manifest parses, every declared route is `GET` and namespaced under its own
  `/m/<key>/` prefix, and each read-model module declares its known routes. This catches the drift a future module
  introduces by adding an `IModuleApi` and forgetting to publish it in the manifest — exactly the gap closed here.

Notes
- Pure declarative change on the module side (manifests are data, not code): no handler, endpoint or core change. The
  routes were already live and tested; this makes them advertised. Closes the Experience read loop end to end —
  discover feeds via `/modules/api`, resolve the tenant at the edge, read each feed through the gateway.

### Sprint 68 — Experience: Dashboard operations-board read API (the whole factory in one call) (2026-07-20)

Added
- **`dashboard`** — the live per-tenant operations board (kept current by consuming `OeeCalculated`,
  `SafetyStandDownTriggered`, `QualityAlertRaised` and `LowStockDetected`) gains a tenant-scoped read API mounted by
  the gateway under `/m/dashboard/*`:
  - `GET /m/dashboard/board[?level=Critical]` — the aggregated wall feed in one call: latest OEE per machine
    (ordered by machine id) and the recent alert feed (newest first). An optional `level` filter narrows the feed,
    while `criticalAlertCount` stays a whole-board headline regardless of the filter.
  - `GET /m/dashboard/summary` — at-a-glance counts: machines tracked, machines below target, recent alerts and
    critical alerts.
  - Both take the tenant from the ambient `ITenantContext` and are guarded with `.RequireTenant()` (400 with no
    resolvable tenant). The plugin references the modules' *events* (shared vocabulary), never the modules.
- **`dashboard`** — `sample.config.json` documents the two routes, the `level` filter and the tenant supply.

Notes
- Caps the Experience read layer: the four operations feeds (`oee`, `maintenance`, `warehouse`, `quality`) are now
  joined by one cross-module aggregate — a wall screen reads the whole factory from a single tenant-scoped endpoint
  without any module-to-module call and with zero core plumbing. The board stays event-driven end to end (Law 4):
  writes arrive only on the bus, reads leave only through the gateway.

### Sprint 67 — Experience: Quality defect-rate read API (per-line rates become queryable) (2026-07-20)

Added
- **`quality`** — the defect-rate store gains a tenant-scoped read model (`IDefectRateWindowStore.ForTenant`,
  `QualityLineSnapshot`): the current rolling window of every tracked line-product aggregate, alongside the existing
  `Fold`. The alerting handler is unchanged.
- **`quality`** — a tenant-scoped read API mounted by the gateway under `/m/quality/*`:
  - `GET /m/quality/lines[?breaching=true]` — per-line current defect rate (inspected/defective/rate), each flagged
    `breachesThreshold`, ordered by line then product; an optional filter narrows to only breaching lines.
  - `GET /m/quality/summary` — tracked line count and how many currently breach the threshold.
  - Both take the tenant from the ambient `ITenantContext` and are guarded with `.RequireTenant()`. The breach flag
    is computed by the same `DefectRateEvaluator` the handler uses, so the dashboard and the alerts always agree
    (including the minimum-evidence rule that keeps a cold start from flagging a false breach).
- **`quality`** — `sample.config.json` documents the two routes, the `breaching` filter and the tenant supply.

Notes
- Completes the operations read-model quartet (`oee`, `maintenance`, `warehouse`, `quality`), each serving its read
  model through the identical gateway + tenant pattern — the wall dashboard now has four tenant-scoped feeds and the
  core carries no per-module plumbing for any of them.

### Sprint 66 — Experience: Warehouse stock read API (on-hand levels and low-stock become queryable) (2026-07-20)

Added
- **`warehouse`** — a tenant-scoped read API mounted by the gateway under `/m/warehouse/*`:
  - `GET /m/warehouse/stock[?belowReorder=true]` — the tenant's per-item on-hand levels, ordered by warehouse then
    SKU, each row carrying the reorder point in force (explicit, else the configured positive default) and a
    `belowReorder` flag; an optional filter narrows to only items at/below their reorder point.
  - `GET /m/warehouse/summary` — tracked-item count and how many are at/below reorder.
  - Both take the tenant from the ambient `ITenantContext` and are guarded with `.RequireTenant()`.
- **`warehouse`** — `sample.config.json` documents the two routes, the `belowReorder` filter and the
  header-preferred / query-fallback tenant supply.

Notes
- Third operations module to serve its read model through the identical gateway + tenant pattern (after `oee`,
  `maintenance`). The low-stock view is a level check (on-hand ≤ reorder point) using the same threshold semantics
  as the module's edge-triggered `LowStockDetected` alerting, so the dashboard and the alerts agree on "low".

### Sprint 65 — Experience: Maintenance work-order read API (the backlog becomes queryable) (2026-07-20)

Added
- **`maintenance`** — a tenant-scoped read API mounted by the gateway under `/m/maintenance/*`:
  - `GET /m/maintenance/workorders[?status=Open]` — the tenant's work-order backlog, soonest-due first then by
    number, with an optional case-insensitive `status` filter.
  - `GET /m/maintenance/summary` — total work orders and per-status counts, ordered by status.
  - Both take the tenant from the ambient `ITenantContext` and are guarded with `.RequireTenant()` (a missing
    tenant is `400`).
- **`maintenance`** — `sample.config.json` documents the two routes, the `status` filter and the header-preferred /
  query-fallback tenant supply.

Notes
- The maintenance to-do list — fed only by `EnergySpikeDetected` and `RuleTriggered` on the bus — is now readable by
  a technician's screen or the wall dashboard without referencing the modules that raise the orders. Second
  operations module to serve its read model through the identical gateway + tenant pattern (after `oee`).

### Sprint 64 — Experience: OEE read API (the first operations module serves its read model) (2026-07-20)

Added
- **`oee`** — a tenant-scoped read API mounted by the gateway under `/m/oee/*`, the first business/operations module
  to expose its read model over HTTP:
  - `GET /m/oee/snapshots` — the tenant's per-machine OEE snapshots (availability, performance, quality, OEE),
    ordered by machine then period, each row flagged `meetsTarget` against the configured `targetOee`.
  - `GET /m/oee/summary` — a factory-wide rollup: snapshot count, mean OEE, and how many fall below target.
  - Both inherit the tenant block (Sprints 60–63): the tenant comes from the ambient `ITenantContext` and each
    route is guarded with `.RequireTenant()`, so a missing tenant is `400` and the handler reads it unconditionally.
- **`oee`** — `sample.config.json` documents the two routes and the header-preferred / query-fallback tenant supply.

Notes
- Feeds the OEE dashboard already declared in the module manifest, and proves the read-API pattern established by
  the platform trio (`deliveryhealth`, `activity`, `notification`) generalizes to an operations module verbatim —
  the module still references only the shared events and the gateway contract, never a connector or another module.

### Sprint 63 — Platform: per-request tenant logging scope ("tenant is always in scope", literally) (2026-07-20)

Added
- **`FactoryOS.Gateway`** — once the tenant-resolution middleware resolves a tenant, the rest of the pipeline
  runs inside an `ILogger` scope carrying it (`TenantScopeKey = "Tenant"`), so **every log line emitted while
  handling the request is stamped with its tenant**. Requests with no resolved tenant open no such scope.
- **`FactoryOS.Api`** — `sample.logging-scopes.json`: a sample `Logging` section that enables scope rendering
  (`Console:IncludeScopes`) so the tenant appears in every log line — multi-tenant activity becomes greppable.

Notes
- Closes the tenant block (Sprints 60–63): the tenant is resolved once at the edge (60), consumed ambiently by
  module read APIs (61), guarded declaratively with `RequireTenant()` (62), and now carried through the logs of
  the entire request (63). The Constitution's invariant "tenant is always in scope" now holds for observability
  too, not just data access — with zero per-module code.

### Sprint 62 — Platform: `RequireTenant()` endpoint filter (the tenant guard becomes declarative) (2026-07-20)

Added
- **`FactoryOS.Gateway`** — `RequireTenantEndpointFilter` and the `RouteHandlerBuilder.RequireTenant()`
  extension: a route declares "a tenant is required here" once, and requests without a resolved tenant are
  rejected with `400 Bad Request` before the handler runs — so the handler reads `ITenantContext.Tenant`
  unconditionally, never re-checking presence.

Changed
- **`deliveryhealth`, `activity`, `notification`** — every read route now chains `.RequireTenant()` and reads
  `context.Tenant` directly. The per-handler `if (no tenant) return BadRequest` branch and the duplicated
  `TenantRequired` constant are gone from all three modules; the guard lives in one filter.

Notes
- The cross-cutting tenant guard is now a single, tested unit instead of a copied idiom. Any future module
  route opts in with one call; the 400 contract is guaranteed identically everywhere, and modules carry no
  tenant-validation plumbing of their own.

### Sprint 61 — Platform: module read APIs adopt the ambient tenant (the `?tenant=` boilerplate is gone) (2026-07-20)

Changed
- **`deliveryhealth`, `activity`, `notification`** — the three module read APIs now read the tenant from the
  ambient `ITenantContext` (`[FromServices]`-injected) that the gateway resolves at the edge, instead of parsing
  and validating a `tenant` query parameter in every handler. A caller supplies the tenant once — via the
  `X-FactoryOS-Tenant` header (preferred) or the `tenant` query fallback — and every route honours it uniformly;
  a missing tenant still returns `400`. The endpoints shrink to their real job: reading a tenant-scoped model.
- **Sample configs** — `deliveryhealth`, `activity` and `notification` document both the header and the query
  fallback for supplying the tenant.

Notes
- Proves Sprint 60 end to end: an operator can now query `/m/deliveryhealth/health` with only the tenant header,
  no query string. The pattern is the template every future module read API inherits — tenant resolution is a
  gateway concern, not a per-endpoint one, keeping modules free of cross-cutting plumbing.

### Sprint 60 — Platform: gateway tenant resolution (the tenant is resolved once, at the edge) (2026-07-20)

Added
- **`FactoryOS.Gateway`** — a request-scoped `ITenantContext` (`HasTenant`, `Tenant`, `TryGetTenant`) plus a
  `TenantResolutionMiddleware` that resolves each request's tenant **once, at the edge**, from a configurable
  header (`X-FactoryOS-Tenant`) with a query-string fallback (`tenant`). A module endpoint reads the ambient,
  already-validated tenant instead of re-parsing and re-validating a `tenant` parameter on every route.
- **`FactoryOS.Gateway`** — `AddTenantResolution(configure?)` (scoped context + `TenantResolutionOptions`,
  last-wins so config overrides the gateway default), `UseTenantResolution()` for the pipeline, and a discovery
  endpoint `GET /tenant` returning `{ resolved, tenant }` — a client can verify exactly which tenant the gateway
  sees. `AddModuleGateway` wires the default; the API host binds `Gateway:TenantResolution` from configuration.
- **`FactoryOS.Api`** — `sample.tenant-resolution.json`: a sample `Gateway:TenantResolution` section (header and
  query-fallback keys are configuration; the tenant itself always arrives on the request, never in the file).

Notes
- Multi-tenancy stays a construction rule: the header name is configuration, never hard-coded, and the middleware
  never rejects — whether a tenant is *required* remains the endpoint's decision, so tenant-agnostic routes such as
  `/modules` are unaffected. The `/tenant` handler is `[FromServices]`-bound so an under-wired host degrades only
  that one endpoint, never the whole routing table. Establishes the ambient-tenant pattern that future module read
  APIs adopt in place of the explicit `?tenant=` parameter.

### Sprint 59 — Experience: manifest-driven API discovery (the read APIs become findable as data) (2026-07-20)

Added
- **`FactoryOS.Contracts`** — a new `PluginApiRoute` (`method`, `path`, `query[]`, `description`) and a
  `PluginManifest.Api` list: a plugin's HTTP read routes are now **declared in the manifest as data, not code**.
- **`FactoryOS.Plugin`** — `PluginManifestReader` parses the `api` array (validating non-empty `method` and `path`,
  rejecting with `Plugin.Manifest.InvalidApiRoute`) and defaults it to empty when absent.
- **`FactoryOS.Gateway`** — a new discovery endpoint `GET /modules/api` returns each **active** module's declared
  read routes (`ModuleApiSummary`: key, name, routes), ordered by key — mirroring `/modules/ui`. Disabled/failed
  modules and modules that declare no routes are excluded, so the catalogue reflects exactly the reachable read
  surface. A frontend or an agent can now discover the four module read APIs (deliveryhealth, activity,
  notification, …) by manifest alone, never by referencing a plugin by name.

Notes
- Contracts over names, end to end: the route is *served* by the plugin's `IModuleApi` and *declared* in its
  manifest; the gateway aggregates the declarations without knowing any plugin. Establishes the discovery pattern
  every future module read API inherits for free.

### Sprint 58 — Experience: Notification outbox read API (dispatched-notification history over HTTP) (2026-07-20)

Added
- **`plugins/notification`** — the per-tenant outbox of dispatched notifications is now **queryable over HTTP**,
  extending the module-read-API pattern to a third module. The gateway mounts the endpoint under
  `/m/notification/*` from the manifest key:
  - `GET /m/notification/outbox?tenant=<t>&max=<n>` — a tenant's dispatched-notification history (channel,
    transport, priority, subject, action, dispatchedAt), **newest first**; `max` defaults to 50 and is clamped to
    `[1, 200]`. A missing/blank `tenant` returns `400 Bad Request`.
  - The API is **read-only** — the outbox is written solely by the module's dispatch handlers. An operator now sees
    what was routed and to which transport without referencing the transport connectors, completing the
    notification pipeline's read surface (requested → dispatched → **queryable** → delivered → measured).
- **`NotificationApi`** implements the gateway's `IModuleApi` via minimal APIs; `NotificationPlugin` registers it as
  an `IModuleApi` singleton, `module.json` declares the route under `api`, and `provides` gains `notification.api`.

### Sprint 57 — Experience: Activity Feed read API (the factory timeline over HTTP) (2026-07-20)

Added
- **`plugins/activity`** — the factory timeline is now **queryable over HTTP**, applying the module-read-API pattern
  established in Sprint 56 to a second module. The gateway mounts the Activity endpoint under `/m/activity/*` purely
  from the manifest key:
  - `GET /m/activity/feed?tenant=<t>&max=<n>` — a tenant's most recent activity entries (category, headline,
    occurredAt, sourceEventId), **newest first**; `max` is clamped to `[1, FeedCapacity]`. A missing/blank `tenant`
    returns `400 Bad Request`.
  - The API is **read-only** — the feed is fed exclusively by the noteworthy integration events the module already
    consumes (rules, work orders, safety stand-downs, quality alerts, delivery degradation). A UI or an operator can
    read the live chronological record without referencing any producing module.
- **`ActivityApi`** implements the gateway's `IModuleApi` contract via minimal APIs; `ActivityPlugin` registers it as
  an `IModuleApi` singleton, `module.json` declares the route under `api`, and `provides` gains `activity.api`.

### Sprint 56 — Experience: Delivery Health read API (the first module HTTP surface through the gateway) (2026-07-20)

Added
- **`plugins/deliveryhealth`** — the observability read model is now **queryable over HTTP**. Delivery Health is the
  **first module to implement `IModuleApi`**, so the API gateway mounts its endpoints under the reserved per-module
  prefix `/m/deliveryhealth/*` purely from the manifest key — never by name, zero core diff:
  - `GET /m/deliveryhealth/health?tenant=<t>` — a tenant's per-transport tallies (attempts, delivered, failed),
    ordered by transport.
  - `GET /m/deliveryhealth/failures?tenant=<t>&max=<n>` — the tenant's most recent failed deliveries, newest first;
    `max` is clamped to `[1, RecentFailureCapacity]`. A missing/blank `tenant` returns `400 Bad Request`.
  - The API is **read-only** — the model is fed exclusively by `NotificationDelivered` on the bus. An operator, a UI
    or an AI agent can now judge transport health without referencing the connectors or the Notification module,
    closing the observability spine's read side (dispatched → delivered → measured → alerted → **queryable**).
- **`DeliveryHealthApi`** contributes endpoints via minimal APIs behind the gateway's `IModuleApi` contract; the
  plugin registers it as an `IModuleApi` singleton in `ConfigureServices`, and `module.json` now declares the two
  routes under `api`. The manifest `provides` gains `deliveryhealth.api`.

Notes
- Contracts over names: mounting is driven entirely by the manifest key and plugin state — a disabled or removed
  Delivery Health plugin simply is not mounted, with no core change. Establishes the pattern for every future module
  read API.

### Sprint 55 — Platform: delivery-degradation alerting (from measured health to a raised signal) (2026-07-20)

Added
- **`plugins/deliveryhealth`** — the read model now *acts*: when a transport's consecutive-failure streak reaches the
  configured threshold, `NotificationDeliveredHandler` raises `DeliveryHealthDegraded` on the bus — **exactly once per
  crossing**, since any success resets the streak (so a persistently failing transport does not spam alerts, yet a
  recovered-then-failing one alerts again):
  - The store now tracks a per-transport consecutive-failure streak (reset by any success) and `Record` returns an
    atomic `RecordOutcome` snapshot (recorded?, tallies, streak) captured **under the tenant lock**, so the
    decision to alert is race-free.
  - `DeliveryHealthOptions.FailureThreshold` (default 3) sets the streak at which a transport is declared degraded.
  - New shared event `DeliveryHealthDegraded` (tenant, transport, streak, tallies, last detail).
- **`plugins/activity`** — the Activity Feed consumes `DeliveryHealthDegraded` (a fifth subscriber), folding a degraded
  transport into the factory timeline under category `Delivery`, so the alert surfaces where operators already look —
  no emitted-but-unconsumed signal.

Verification
- `dotnet build -c Release` → **0 warnings / 0 errors**.
- `dotnet test` → **595 passed** (519 unit + 72 integration + 4 architecture), 0 failed.
  - `DeliveryHealthAlertTests` — alert once at the crossing, no re-alert past it, success resets so it re-alerts, healthy never alerts.
  - `DeliveryHealthStoreTests` — streak-and-reset added alongside the Sprint 54 tallies/failures/idempotency/isolation.
  - `DeliveryDegradationChainTests` — over the real bus: `NotificationDelivered×N → DeliveryHealthDegraded → activity entry`.

### Sprint 54 — Platform: Delivery Health read model (closing the notification audit trail on the read side) (2026-07-20)

Added
- **`plugins/deliveryhealth`** — a new observability plugin and the first consumer of `NotificationDelivered`, which
  was emitted by the outbound connectors but had no reader. It folds each delivery outcome into a per-tenant read
  model without referencing the connectors or the Notification module, closing the audit trail: **dispatched →
  delivered → observed**:
  - `IDeliveryHealthStore` / `InMemoryDeliveryHealthStore` — per-transport tallies (`TransportHealth`: attempts,
    delivered, failed) that accumulate unbounded (one entry per transport) plus a bounded, newest-first list of
    recent failure details (`DeliveryFailure`) for troubleshooting. Per-tenant `Lock`-guarded state; idempotent by
    the delivery event's id so a redelivery never double-counts.
  - `NotificationDeliveredHandler` — subscribes to `NotificationDelivered` and folds the outcome in.
  - `DeliveryHealthOptions` — `RecentFailureCapacity` (default 50) bounds the retained failure list.
- Registered `plugins/deliveryhealth` in `FactoryOS.slnx` and both test projects.

Verification
- `dotnet build -c Release` → **0 warnings / 0 errors**.
- `dotnet test` → **590 passed** (515 unit + 71 integration + 4 architecture), 0 failed.
  - `DeliveryHealthStoreTests` — per-transport tallies ordered, newest-first bounded failures, idempotency, tenant isolation.
  - `DeliveryHealthChainTests` — the audit trail closed over the real bus: `NotificationDispatched →
    NotificationDelivered → delivery-health tally`, log connector and Delivery Health composing only through shared contracts.

### Sprint 53 — Platform: assistant-answer notifications (AI answers delivered through the notification door) (2026-07-20)

Added
- **`plugins/notification`** — the Notification module now consumes `BrainAnswered` alongside `WorkflowActionRequested`
  and `ReportGenerated`, so a Company Brain answer is delivered through the **same door** as any other notification —
  without referencing the AI layer:
  - `BrainAnsweredHandler` — routes the configured assistant channel to a transport (reusing `TransportResolver`),
    records the dispatch in the same per-tenant outbox and announces `NotificationDispatched` with a subject carrying
    the answer and its citation count. Delivery over a real transport stays a connector's job. Idempotent by the
    answer event's id: redelivery neither records a duplicate nor re-announces.
  - `NotificationOptions` gained `AssistantNotificationChannel` (default `assistant`), `AssistantNotificationPriority`
    (`Normal`) and `AssistantNotificationAction` (`Notify`).
- `sample.config.json` maps `assistant → chat` and carries the assistant-notification settings.

Verification
- `dotnet build -c Release` → **0 warnings / 0 errors**.
- `dotnet test` → **585 passed** (511 unit + 70 integration + 4 architecture), 0 failed.
  - `BrainAnsweredHandlerTests` — routing, default-transport fallback, and once-only dispatch on redelivery.
  - `BrainAnswerToNotificationChainTests` — the conversational-AI loop delivered over the real bus:
    `BrainQuestionAsked → BrainAnswered → NotificationDispatched`, the Brain Query agent (real Company Brain, RAG +
    stubbed LLM) and Notification composing only through shared contracts.

### Sprint 52 — Platform: report-ready notifications (closing the reporting pipeline with a delivery) (2026-07-20)

Added
- **`plugins/notification`** — the Notification module now consumes `ReportGenerated` alongside `WorkflowActionRequested`,
  closing the Scheduler → Reporting → File Storage pipeline (Sprints 48–50) with a delivery — without ever
  referencing the Reporting module or the object store:
  - `ReportGeneratedHandler` — routes the configured report channel to a transport (reusing `TransportResolver`),
    records the dispatch in the same per-tenant outbox and announces `NotificationDispatched`. Actual delivery over
    a real transport stays a connector's job (connectors are the only door out). Idempotent by the report event's
    id: redelivery of the same report neither records a duplicate nor re-announces.
  - `NotificationOptions` gained `ReportNotificationChannel` (default `reports`), `ReportNotificationPriority`
    (`Normal`) and `ReportNotificationAction` (`Notify`) — report routing is **data, not a customer branch**; the
    factory decides its `reports` channel goes to `email` purely by configuration.
- `sample.config.json` maps `reports → email` and carries the report-notification settings.

Verification
- `dotnet build -c Release` → **0 warnings / 0 errors**.
- `dotnet test` → **581 passed** (508 unit + 69 integration + 4 architecture), 0 failed.
  - `ReportGeneratedHandlerTests` — routing, default-transport fallback, and once-only dispatch on redelivery.
  - `ReportToNotificationChainTests` — the full four-plugin pipeline over the real bus: `SchedulerTick → … →
    ReportGenerated → NotificationDispatched`, Scheduler/Reporting/File Storage/Notification composing only through
    shared contracts.

### Sprint 51 — AI: Brain Query agent (conversational Q&A over the bus) (2026-07-20)

Added
- **`agents/brain`** — the AI worker that closes the RAG loop opened in Sprint 48: it answers questions posed on
  the bus from the tenant's knowledge base, so **asking is decoupled from answering** and any producer (a UI, a
  rule, an automated check) can pose a question without referencing the AI layer:
  - `BrainQuestionAskedHandler` — asks the `ICompanyBrain` facade (RAG retrieval + LLM generation, both over HTTP,
    never in-process) and re-enters the grounded, cited answer as `BrainAnswered`. A Brain failure throws so the
    bus retries and can dead-letter; the question is marked answered only after success, so redelivery is a no-op.
  - `BrainQueryAgentOptions` — the chat model, embedding model and retrieval depth are **configuration, not the
    question**: the asker poses a question, the factory chooses how the Brain answers (the embedding model must
    match how the base was indexed).
  - `BrainQueryAgentPlugin`; `agent.json` (`kind: agent`, consumes `BrainQuestionAsked`, emits `BrainAnswered`) +
    `sample.config.json`.
- **Shared events `BrainQuestionAsked`** (tenant, question, asked-by, asked-at) and **`BrainAnswered`** (tenant,
  question, answer, model, citations, answered-at, source event id) in `FactoryOS.Contracts/Events`.

Verification
- `dotnet build -c Release`: 0 warnings, 0 errors.
- `dotnet test`: **577 passed** (505 unit + 68 integration + 4 architecture), 0 failed.
  New: `BrainQuestionAskedHandlerTests` (configured models drive the question, answer announced, failure throws,
  redelivery once), `BrainQueryChainTests` (a question on the bus is answered from **indexed knowledge** via a
  real `CompanyBrain` — retrieval + a stubbed LLM — and comes back cited to its source).

### Sprint 50 — Scheduled OEE report artifact (Scheduler → Reporting → File Storage) (2026-07-20) 🏁 halfway

Added
- **Reporting now renders and stores a CSV artifact on a schedule** — closing `SchedulerTick → ScheduledTaskDue →
  Reporting → object store → ReportGenerated`, three plugins composed only through shared contracts:
  - `OeeCsvRenderer` — pure, deterministic render of the OEE read-model to CSV (header + one row per machine·day,
    machines ordered by id, newest day first); same state → byte-identical document.
  - `ScheduledTaskDueHandler` — on the configured report action, renders the tenant's OEE history and writes it to
    the `IObjectStore` under a stable key (`reports/oee/{scheduleId}.csv`), then announces `ReportGenerated`.
    Stores before marking processed, so a storage failure retries; a redelivery re-stores the identical artifact
    but announces once.
  - `IOeeReport.Machines(tenant)` added so a report can enumerate a tenant's machines (read-model, tenant-scoped).
  - `ReportingOptions` gains `ReportAction` and `ReportKeyPrefix` (data-configurable); `module.json` now
    `consumes: [OeeCalculated, ScheduledTaskDue]`, `emits: [ReportGenerated]`, and declares
    `requires: [filestorage.objects]`; `sample.config.json` updated.
- **Shared event `ReportGenerated`** (`FactoryOS.Contracts/Events`): a report artifact was rendered and stored —
  carries tenant, report id, object key, content type, size, row count, generated-at and source event id.
- First consumer of the Sprint 49 `IObjectStore`: Reporting persists blobs without any direct storage dependency,
  the object-store swappable behind the contract.

Verification
- `dotnet build -c Release`: 0 warnings, 0 errors.
- `dotnet test`: **573 passed** (502 unit + 67 integration + 4 architecture), 0 failed.
  New: `OeeCsvRendererTests` (header + ordered rows, empty tenant, determinism), `ScheduledTaskDueHandlerTests`
  (report action stores + announces, foreign action no-op, redelivery announces once), `ScheduledReportChainTests`
  (a due schedule renders and stores a tenant-scoped CSV over the real bus).

### Sprint 49 — Platform: File Storage (the object-store door) (2026-07-20)

Added
- **`IObjectStore` contract** (`FactoryOS.Contracts/Storage`) — the Platform-layer door to blob storage, abstracting
  MinIO/S3 the way `IEventBus` abstracts the broker. Tenant-scoped by construction: every operation
  (`PutAsync`/`GetAsync`/`ExistsAsync`/`ListAsync`) takes the tenant explicitly; no code path reads or writes across
  tenants. `StoredObject` (tenant, key, content-type, bytes, size) and `ObjectRef` (listing metadata without bytes).
- **`plugins/filestorage`** — the provided capability: `InMemoryObjectStore` (per-tenant, lock-guarded buckets;
  put replaces by key; prefix-filtered, key-ordered listing; configurable object-size cap) plus `FileStoragePlugin`
  registering it as `IObjectStore`. A module persists reports, attachments and exports without a direct storage
  dependency; a MinIO/S3-backed store swaps in behind the interface untouched. `module.json` + `sample.config.json`.
- Modeled as a **provided service** (like the event bus and knowledge store), not an event consumer — the honest
  shape for infrastructure the platform owns; installing or removing the folder adds or removes blob storage with
  zero core changes.

Verification
- `dotnet build -c Release`: 0 warnings, 0 errors.
- `dotnet test`: **566 passed** (496 unit + 66 integration + 4 architecture), 0 failed.
  New: `InMemoryObjectStoreTests` (round-trip, overwrite, missing→null, exists, tenant isolation, prefix listing
  + ordering, size-limit rejection, unlimited), `FileStoragePluginTests` (the plugin provides a working,
  tenant-isolated store through the host container).

### Sprint 48 — AI: Knowledge Ingest agent (live events → Company Brain memory) (2026-07-20)

Added
- **`agents/knowledge`** — the bridge that gives the existing Company Brain RAG a memory of live operations: an
  AI digital worker that narrates noteworthy events into knowledge documents and ingests them through the
  `IKnowledgeIndexer` (chunk → embed → store), so the Brain can later retrieve and **cite** what happened on the
  floor. AI is reached only through the embedding gateway the indexer owns — HTTP to a provider, never in-process:
  - `KnowledgeIngestor` — the single ingest path: builds a tenant-scoped `KnowledgeDocument` and indexes it.
    **Idempotent by construction** — the document `Source` is derived from the producing event's id, so chunk ids
    (`Source#i`) are stable and storage upserts rather than duplicates (same shape as Sprint 45's deterministic
    work-order numbering). An indexer failure throws so the bus retries and can dead-letter; a fact is not lost.
  - Four handlers — `RuleTriggered`, `WorkOrderCreated`, `SafetyStandDownTriggered`, `QualityAlertRaised` — each
    narrates its event into a plain-text sentence (with tenant, timestamp and key figures) under a categorized
    source (`activity/rule/…`, `activity/workorder/…`, …). Each references only the shared event, never a module.
  - `KnowledgeAgentPlugin`, `KnowledgeIngestOptions` (logical embedding model, data-configurable); `agent.json`
    (`kind: agent`, consumes the four, emits none) + `sample.config.json`.
- Same four events now feed **three** independent subscribers (Maintenance/Workflow, Activity Feed, Knowledge
  Ingest) off one publish — bus fan-out at work, with the AI memory decoupled from every producer.

Verification
- `dotnet build -c Release`: 0 warnings, 0 errors.
- `dotnet test`: **556 passed** (487 unit + 65 integration + 4 architecture), 0 failed.
  New: `KnowledgeIngestTests` (document shape + configured model, failure throws, each event narrated under its
  source), `KnowledgeIngestChainTests` (a reading crosses a threshold → the fired rule is retrievable knowledge
  for the asking tenant and unreachable for any other, via the real indexer over a fake embedding gateway).

### Sprint 47 — Experience: Activity Feed (the factory timeline) (2026-07-20)

Added
- **`plugins/activity`** — the factory timeline: a per-tenant, newest-first read-model that folds noteworthy
  events into one uniform, human-readable feed. A live chronological record that **complements the existing
  Company Brain RAG** (the knowledge base in `FactoryOS.Ai`), rather than duplicating it:
  - `IActivityFeed` / `InMemoryActivityFeed` — per-tenant, lock-guarded, newest-first, **bounded** to a configured
    capacity; recording is idempotent by the producing event's id, so at-least-once delivery never doubles a line.
  - `ActivityEntry` — the normalized line (tenant, category, headline, occurred-at, source event id) every handler
    maps its event into, so the feed works against one shape regardless of origin.
  - Four handlers — `RuleTriggered → Rule`, `WorkOrderCreated → Maintenance`, `SafetyStandDownTriggered → Safety`,
    `QualityAlertRaised → Quality` — each references only the shared event, never the emitting module.
  - `ActivityPlugin`, `ActivityOptions` (feed capacity); `module.json` (consumes the four events, emits none) +
    `sample.config.json`.
- **Bus fan-out reconfirmed end-to-end**: `RuleTriggered` and `WorkOrderCreated` are now each consumed by *two*
  independent subscribers (Maintenance/Workflow and the Activity Feed) off one publish, with no shared reference.

Notes
- Discovered `CompanyBrain` already exists as the RAG Q&A engine in `FactoryOS.Ai`; this module was named **Activity
  Feed** to complement (not shadow) it — a future sprint can ingest feed entries into the Brain's knowledge store.

Verification
- `dotnet build -c Release`: 0 warnings, 0 errors.
- `dotnet test`: **549 passed** (481 unit + 64 integration + 4 architecture), 0 failed.
  New: `InMemoryActivityFeedTests` (newest-first, idempotent, bounded capacity, tenant isolation, empty read),
  `ActivityHandlersTests` (each event → its categorized entry), `ActivityFeedTests` (feed populated via bus fan-out).

### Sprint 46 — Workflow escalates work orders (the full automation spine) (2026-07-20)

Added
- **`plugins/workflow` now consumes `WorkOrderCreated`** — extending the automation spine one hop to a
  human-facing action and completing the end-to-end chain, all event-driven with **no module referencing
  another**:
  `MeterReadingReceived → Rule Engine → RuleTriggered → Maintenance → WorkOrderCreated → Workflow →
  WorkflowActionRequested (→ Notification → delivery)`.
  - `WorkOrderCreatedHandler` — normalizes a raised work order into the module's uniform `WorkflowSignal` and
    funnels it through the existing `WorkflowEngine`, so a `WorkOrderCreated` is matched against the tenant's
    declarative rules exactly like every other trigger. No new engine logic, no idempotency logic — it reuses
    the one place actions are emitted and the shared processed-event log.
  - `module.json` now declares `WorkOrderCreated` among `consumes`; `sample.config.json` gains a
    `WorkOrderCreated → Notify` rule on the `maintenance` channel — pure configuration.
- The Rule Engine (Sprint 44), Maintenance rule reaction (Sprint 45) and this Workflow hop now form a four-plugin
  chain proven end-to-end over a single bus, with each plugin removable to drop exactly its own hop.

Verification
- `dotnet build -c Release`: 0 warnings, 0 errors.
- `dotnet test`: **539 passed** (472 unit + 63 integration + 4 architecture), 0 failed.
  New: `WorkOrderCreatedHandlerTests` (mapped-action requested, no-rule-no-action, redelivery once),
  `ReadingToWorkflowSpineTests` (a single reading flows through four plugins to a notification request).

### Sprint 45 — Maintenance reacts to fired rules (closes the automation loop) (2026-07-20)

Added
- **`plugins/maintenance` now consumes `RuleTriggered`** — closing the end-to-end automation loop
  `MeterReadingReceived → Rule Engine → RuleTriggered → Maintenance → WorkOrderCreated` with **no module
  referencing another**, only the shared event vocabulary:
  - `RuleTriggeredHandler` — when a fired rule requests one of the module's configured maintenance actions, raises
    a corrective work order and emits `WorkOrderCreated`. Foreign actions are ignored; the module reacts to data,
    not to a customer branch.
  - `RuleWorkOrderFactory` — pure, deterministic build of the `WorkOrder` from a trigger; the number is derived
    from the **trigger's own event id**, so redelivery is a no-op while two rules firing on one reading (distinct
    triggers) yield two distinct orders.
  - `MaintenanceOptions` extended with `RuleActions` (the action set the module owns, matched case-insensitively),
    `RuleWorkOrderPrefix` and `RuleWorkOrderDueInHours` — all configuration, no code branch.
  - `MaintenancePlugin` registers the new handler alongside the existing `EnergySpikeDetected` one; `module.json`
    now declares `consumes: [EnergySpikeDetected, RuleTriggered]`; `sample.config.json` updated.
- Reused the existing idempotent `IWorkOrderStore` (add-by-number) so both spike- and rule-triggered orders share
  one duplicate-free store.

Verification
- `dotnet build -c Release`: 0 warnings, 0 errors.
- `dotnet test`: **535 passed** (469 unit + 62 integration + 4 architecture), 0 failed.
  New: `RuleTriggeredHandlerTests` (owned-action creates + announces, case-insensitive, foreign-action ignored,
  redelivery once, two-triggers-two-orders), `RuleWorkOrderFactoryTests` (deterministic number, due/target),
  `RuleToMaintenanceChainTests` (reading → work order over the real bus).

### Sprint 44 — Platform: Rule Engine (2026-07-20)

Added
- **`plugins/ruleengine`** — the Platform-layer turn from observation to action: declarative metric-threshold
  rules, evaluated against the Standard Model reading stream, emitting normalized action requests. Rules are
  **data, never code** — a factory tunes automation purely by configuration, the direct expression of the
  "mapping is a manifest, not code" invariant applied to automation:
  - `RuleDefinition` (id, metric, `ComparisonOperator`, threshold, action) + `RuleEngineOptions` — the rule set
    is configuration; the six comparators (`>`, `>=`, `<`, `<=`, `==`, `!=`) are the fixed platform vocabulary.
  - `RuleEvaluator.Matches` — the pure, total decision at the core; no tenant, clock or I/O, so the entire
    operator table is exhaustively testable.
  - `MeterReadingReceivedHandler` — consumes `MeterReadingReceived`, fires every rule whose metric matches
    (case-insensitive) and whose comparison holds, emitting `RuleTriggered`. References the shared events and the
    evaluator only — never a consuming module; who acts on a trigger is entirely decoupled.
  - `IRuleFiringLog` / `InMemoryRuleFiringLog` — idempotency widened from event id to the **(rule, reading)**
    pair, so one reading may fire several rules yet each fires exactly once under at-least-once redelivery.
  - `RuleEnginePlugin`; `module.json` (consumes `MeterReadingReceived`, emits `RuleTriggered`) + `sample.config.json`.
- **Shared event `RuleTriggered`** (`FactoryOS.Contracts/Events`): a rule matched an observation — carries tenant,
  rule id, metric/meter, value, operator, threshold, action, trigger instant and source event id for consumers.
- **Integration test** (`RuleEngineTriggerTests`): over the real bus, a reading crossing a configured threshold
  emits one `RuleTriggered`; a redelivery of the same reading and a below-threshold reading emit nothing more —
  the observation-to-action turn travels the bus with producer and consumer fully decoupled.

Verification
- `dotnet build -c Release`: 0 warnings, 0 errors.
- `dotnet test`: **527 passed** (462 unit + 61 integration + 4 architecture), 0 failed.
  New: `RuleEvaluatorTests` (operator table + unknown-operator throw), `MeterReadingReceivedHandlerTests`
  (match/no-match/other-metric/case-insensitive/multi-rule/redelivery), `RuleEngineTriggerTests` (bus chain).

### Sprint 43 — Integration: Webhook outbound connector (2026-07-20)

Added
- **`connectors/webhook`** — the second outbound connector, delivering over real HTTP, proving the Sprint 40
  outbound contract generalizes beyond the log sink and that transports **multiplex by name**:
  - `WebhookConnector : IOutboundConnector` — resolves the message's channel to a per-channel endpoint and POSTs;
    a missing endpoint is a failed result, not a throw.
  - `EndpointResolver` — pure channel→URL routing (case-insensitive, configured fallback, absolute-URL validation).
  - `IWebhookSender` / `HttpWebhookSender` — the network port, abstracted so routing/idempotency test without HTTP;
    the HTTP impl maps success status → delivered, non-success and transport errors → failed (never an unhandled throw).
  - `NotificationDispatchedHandler` — the bus bridge, depending on the **concrete** `WebhookConnector` (not the
    shared `IOutboundConnector`) so it runs alongside the log connector without DI ambiguity; ignores foreign
    transports, dedupes by dispatch event id, emits `NotificationDelivered`.
  - `WebhookConnectorOptions` (transport, per-channel URLs with `${secret:...}` placeholders, default URL);
    `WebhookConnectorPlugin` wires the connector, sender, HTTP client and bridge; `connector.json` +
    `sample.config.json` provided.
- **Multiplex integration test** (`OutboundTransportMultiplexTests`): log and webhook connectors installed side
  by side over the real bus — a `webhook` dispatch is POSTed by webhook only, a `log` dispatch is journaled by
  log only, each emitting one `NotificationDelivered`. Transports compose with no central router.

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **507 passed** (443 unit + 60 integration + 4 architecture), 0 failed. New coverage:
  endpoint resolution (mapped/default/none/invalid), connector POST + no-endpoint failure, HTTP sender
  success/non-success/transport-error mapping, bridge transport-match/ignore/idempotency, and the two-connector
  transport multiplex through the real bus.

### Sprint 42 — AI: Insight Agent (2026-07-20)

Added
- **`InsightGenerated`** in `FactoryOS.Contracts/Events` — AI output re-enters the system as just another event
  on the bus (trigger type, subject, insight text, model), never an in-process call.
- **`agents/insight`** — the first AI digital worker, wired like any plugin but reaching a model only through the
  LLM Gateway (the AI analogue of the connector door):
  - `InsightPromptBuilder` — pure, deterministic construction of a tenant-scoped `ChatCompletionRequest` from a
    signal and options, so the agent's prompting is testable without a model.
  - `InsightEngine` — one generic reasoning path: prompt → `ILlmGateway.CompleteAsync` → emit `InsightGenerated`.
    Marks the trigger processed only after a successful generation and **throws on gateway failure so the bus
    retries/dead-letters** — an insight is never silently lost; once emitted, redelivery is a no-op.
  - Two normalizing handlers (`SafetyStandDownTriggered`, `QualityAlertRaised`) map their events into one uniform
    `InsightSignal`, referencing the shared events, never the producing modules.
  - `InsightAgentOptions` (model, system prompt, temperature, max tokens — all data); `InsightAgentPlugin` wires
    the engine and handlers; `agent.json` manifest (`kind: agent`, `model`, `consumes`, `emits`);
    `sample.config.json` provided.
- **AI integration test** (`SafetyToInsightChainTests`): a severe incident becomes a stand-down and then an
  AI insight over the real bus with a stubbed gateway — `SafetyIncidentReported → SafetyStandDownTriggered →
  InsightGenerated` — neither plugin referencing the other.

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **494 passed** (431 unit + 59 integration + 4 architecture), 0 failed. New coverage:
  prompt construction (system+user, tenant, sampling), success generation + publish, once-per-trigger
  idempotency, gateway-failure throws-and-publishes-nothing, and the end-to-end alert-to-AI-insight chain
  through the real bus.

### Sprint 41 — Experience: Digital Twin (2026-07-20)

Added
- **`plugins/digitaltwin`** — an Experience-layer read model that mirrors each physical asset's live state,
  assembled purely from the event stream:
  - `IAssetTwinRegistry` / `InMemoryAssetTwinRegistry` — per-tenant, lock-guarded twins; every fold guards
    against out-of-order delivery by keeping the newer observation, so a twin only advances in time.
  - `AssetTwin`, `MetricReading`, `AssetHealth`, `TwinStatus` — immutable twin shapes; `Status` is *derived*
    (Online / Degraded / Unknown), never stored, so the twin stays a pure reflection of its events.
  - `MeterReadingReceivedHandler` mirrors telemetry as the latest value per metric; `OeeCalculatedHandler`
    mirrors OEE as asset health. Both dedupe by event id. They reference the shared events, never the producers.
  - `DigitalTwinOptions` (`degradedOeeThreshold`); `DigitalTwinPlugin` wires the registry, idempotency log and
    handlers; `module.json` declares `consumes: [MeterReadingReceived, OeeCalculated]`; `sample.config.json` provided.
- **Integration test** (`DigitalTwinAssemblyTests`): a telemetry reading and an OEE fact from two producers fold
  into one asset's twin over the real bus — latest gauge, health, derived Degraded status and advancing timestamp.

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **489 passed** (427 unit + 58 integration + 4 architecture), 0 failed. New coverage:
  unknown-asset null twin, latest-value-per-metric ordering, out-of-order rejection, degraded vs online status
  derivation, asset/tenant isolation and listing, telemetry and OEE mirroring, redelivery idempotency, and the
  two-source twin assembly through the real bus.

### Sprint 40 — Integration: Outbound connector layer + Log transport (2026-07-20)

Added
- **Outbound connector contract** in `FactoryOS.Contracts/Connectors` — the door *out*, mirroring inbound
  `IConnector`: `IOutboundConnector` (`Key`, `Transport`, `DeliverAsync`), the transport-agnostic `OutboundMessage`,
  and the `OutboundResult` value type (`Ok`/`Failed`). Business modules still never deliver directly — they emit
  facts and a connector carries them across the boundary, keeping "connectors are the only door" true both ways.
- **`NotificationDelivered`** in `FactoryOS.Contracts/Events` — closes the notification audit trail
  (dispatched → delivered, with success and detail).
- **`connectors/log`** — the first outbound connector and the reference every transport (webhook, email, SMS)
  will follow:
  - `LogTransportConnector : IOutboundConnector` — "delivers" by appending to a journal and reporting success;
    its transport name is configurable data (`LogConnectorOptions.Transport`, default `log`).
  - `IDeliveryJournal` / `InMemoryDeliveryJournal` — the per-tenant, lock-guarded delivery log and read model.
  - `NotificationDispatchedHandler` — the bus bridge: consumes `NotificationDispatched`, **ignores other
    transports**, delivers its own through the connector, dedupes by dispatch event id, and emits
    `NotificationDelivered`. References only shared contracts, never the Notification module.
  - `LogConnectorPlugin` (connectors are plugins too) wires the connector, journal, idempotency log and bridge;
    `connector.json` marks `direction: outbound`, `transport: log`, `consumes/emits`; `sample.config.json` provided.
- **Full-loop capstone test** (`SafetyToDeliveryChainTests`): a severe incident travels four plugins and leaves
  the system through the log transport —
  `SafetyIncidentReported → SafetyStandDownTriggered → WorkflowActionRequested → NotificationDispatched →
  NotificationDelivered` — landing in the delivery journal, none of the four referencing another.

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **479 passed** (418 unit + 57 integration + 4 architecture), 0 failed. New coverage:
  journaled delivery + success result, options-driven transport name, journal newest-first + tenant isolation,
  transport-match delivery, foreign-transport ignore, dispatch idempotency, and the end-to-end five-hop
  alert-to-delivery loop through the real bus.

### Sprint 39 — Platform: Scheduler (2026-07-20)

Added
- **`SchedulerTick`** and **`ScheduledTaskDue`** in `FactoryOS.Contracts/Events` — the host clock pulse and the
  normalized "it is time to do X" fact; connectors and modules consume the latter for periodic work.
- **`plugins/scheduler`** — the Platform-layer scheduling source, with the clock owned by the host, not the modules:
  - `ScheduleEvaluator` — the pure due-decision (never-run → due on first pulse; thereafter due once the interval
    elapses; non-positive interval fires every pulse). No state, no I/O, fully deterministic.
  - `IScheduleClock` / `InMemoryScheduleClock` — per-tenant, lock-guarded last-run state; `TryClaim` is an atomic
    compare-and-set so two concurrent pulses can't double-fire and a redelivered pulse is a no-op.
  - `SchedulerTickHandler` consumes `SchedulerTick`, claims each due schedule and emits `ScheduledTaskDue`.
  - `ScheduleDefinition` / `SchedulerOptions` (`schedules`: id, action, everySeconds) — schedules as data;
    `SchedulerPlugin` wires the clock and handler; `module.json` declares `consumes: [SchedulerTick]`,
    `emits: [ScheduledTaskDue]`; `sample.config.json` ships example schedules.
- **Integration test** (`SchedulerTickTests`): a pulse on the real bus fires a due schedule exactly once, and a
  second pulse within the interval fires nothing — the due-decision travels the bus end to end.

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **472 passed** (412 unit + 56 integration + 4 architecture), 0 failed. New coverage:
  never-run/elapsed/not-yet/every-pulse due decisions, first-claim-then-reclaim, claim-after-interval,
  schedule and tenant independence, multi-schedule announcement, within-interval suppression, post-interval
  re-fire, and the end-to-end pulse-to-due flow through the real bus.

### Sprint 38 — Experience: OEE Reporting rollup (2026-07-20)

Added
- **`plugins/reporting`** — a second Experience-layer read model that turns the OEE event stream into
  per-machine **daily history** (average, best, worst), the CQRS source a report or trend chart renders:
  - `IOeeReport` / `InMemoryOeeReport` — per-tenant, lock-guarded; each machine keeps day buckets in a
    `SortedDictionary`, so the configured retention trims the oldest day first. Redis-swappable, same contract.
  - `OeeDailyStat` — an immutable projection row (day, sample count, average, min, max).
  - `OeeCalculatedHandler` buckets each reading by its **UTC calendar day** and deduplicates by event id so a
    redelivered reading never skews the average or count. References the shared OEE event, never the OEE module.
  - `ReportingOptions` (`retainDays`); `ReportingPlugin` wires the report, idempotency log and handler;
    `module.json` declares `consumes: [OeeCalculated]`; `sample.config.json` provided.
- **Fan-out integration test** (`OeeReportingFanOutTests`): a single `OeeCalculated` feeds **both** the Dashboard
  tile and the Reporting rollup at once over the real bus — proving `GetServices<IEventHandler<T>>` multi-subscriber
  fan-out — with two same-day readings averaging to one daily stat while the board shows the latest tile.

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **461 passed** (402 unit + 55 integration + 4 architecture), 0 failed. New coverage:
  unknown-machine empty, same-day average/min/max, newest-first day ordering, retention trimming, machine and
  tenant isolation, UTC-day bucketing, redelivery idempotency, and the two-consumer fan-out through the real bus.

### Sprint 37 — Platform: Notification routing (2026-07-20)

Added
- **`NotificationDispatched`** in `FactoryOS.Contracts/Events` — the shared fact that a notification was routed
  to a transport for a workflow action; a transport connector consumes it without referencing the module.
- **`plugins/notification`** — the Platform-layer bridge that closes the alert → action → notification loop:
  - `WorkflowActionRequestedHandler` consumes `WorkflowActionRequested`, routes the action's channel to a
    transport, records the dispatch in the outbox and emits `NotificationDispatched` — **leaving real delivery
    (email/SMS/chat) to a connector**, honouring "connectors are the only door to the outside".
  - `TransportResolver` — pure, case-insensitive channel→transport lookup with a configured fallback; routing
    is data, never a customer branch.
  - `INotificationOutbox` / `InMemoryNotificationOutbox` — a per-tenant, lock-guarded outbox (audit + read
    model), idempotent by the action's event id so at-least-once delivery never doubles a notification.
  - `NotificationOptions` (`channelTransports`, `defaultTransport`); `NotificationPlugin` wires the outbox and
    handler; `module.json` declares `consumes: [WorkflowActionRequested]`, `emits: [NotificationDispatched]`;
    `sample.config.json` ships an example routing table.
- **Full-loop integration test** (`SafetyToNotificationChainTests`): a severe incident travels three modules —
  `SafetyIncidentReported → SafetyStandDownTriggered → WorkflowActionRequested → NotificationDispatched` — over
  the real bus, none referencing another, and lands in the outbox on the configured transport.

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **453 passed** (395 unit + 54 integration + 4 architecture), 0 failed. New coverage:
  mapped/unmapped/case-insensitive transport resolution, outbox newest-first ordering, tenant isolation,
  source-event idempotency, routed-transport dispatch, default-transport fallback, redelivery no-op, and the
  end-to-end four-hop Safety → Notification loop through the real bus.

### Sprint 36 — Experience: Dashboard read-model (2026-07-20)

Added
- **`plugins/dashboard`** — the first Experience-layer module and the CQRS read side of FactoryOS: a live,
  per-tenant **operations board** kept current purely by consuming events, ready for a wall dashboard or PWA to
  query without touching any module.
  - `IOperationsBoard` / `InMemoryOperationsBoard` — the tenant-partitioned read model (a machine-OEE tile map
    plus a bounded, newest-first alert feed), each tenant's board guarded by its own lock; the contract is
    Redis-swappable with no consumer change.
  - `OeeTile`, `AlertTile`, `BoardSnapshot`, `AlertLevels` — immutable read-model shapes; alerts are normalized
    to `Critical`/`Warning` so the UI colours a tile the same way whatever raised it.
  - Four consuming handlers — `OeeCalculated` (latest OEE per machine, last-write-wins), `SafetyStandDownTriggered`
    (critical), `QualityAlertRaised` and `LowStockDetected` (warnings) — each deduping by event id so at-least-once
    delivery never double-counts the feed. They reference the shared events, never the producing modules.
  - `DashboardOptions` (`recentAlertCapacity`) tunes feed depth as data; `DashboardPlugin` wires the board,
    idempotency log and handlers; `module.json` declares the four consumed facts and an `emits: []` read model;
    `sample.config.json` provided.
- **Experience integration test** (`DashboardReadModelTests`): an `OeeCalculated` fact and a severe
  `SafetyIncidentReported` (which Safety turns into a stand-down) assemble one cross-module board snapshot over
  the real bus — `SafetyIncidentReported → SafetyStandDownTriggered → board` — with no module referencing the
  Dashboard.

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **444 passed** (387 unit + 53 integration + 4 architecture), 0 failed. New coverage:
  empty board for an unknown tenant, last-write-wins OEE with ordered machines, newest-first + critical-count
  alert feed, capacity-bounded feed eviction, per-tenant isolation, each handler's normalization, feed
  idempotency, and the end-to-end multi-module board assembly through the real bus.

### Sprint 35 — Business Module: Workflow (2026-07-20)

Added
- **`WorkflowActionRequested`** in `FactoryOS.Contracts/Events` — the normalized, configuration-driven bridge
  from any alert to any action (trigger type, subject, action, priority, channel).
- **`plugins/workflow`** — the eleventh business module and the capstone of Phase 3, turning every module's
  alerts into configured actions with zero coupling:
  - `WorkflowRule` / `WorkflowOptions` — declarative rules supplied as data (trigger → action, priority,
    channel); adding or retargeting a rule is a config change, never code.
  - `IWorkflowRuleSet` / `WorkflowRuleSet` — an immutable index of rules by trigger; first-configured wins.
  - `WorkflowSignal` + `WorkflowEngine` — each handler normalizes its specific alert into one uniform signal;
    the engine matches a rule and emits `WorkflowActionRequested` **exactly once** (deduped by the triggering
    event id).
  - Four normalizing handlers — `SafetyStandDownTriggered`, `QualityAlertRaised`, `LowStockDetected`,
    `CertificationGapDetected` — each mapping its event to a signal, referencing the shared events but never
    the producing modules.
  - `WorkflowPlugin` wires the rule set, engine, idempotency log and handlers; `module.json` declares the four
    consumed triggers and `emits: [WorkflowActionRequested]`; `sample.config.json` ships example rules.
- **Capstone cross-module chain** (`SafetyToWorkflowChainTests`): a reported incident becomes a Safety
  stand-down and then a configured Workflow action — `SafetyIncidentReported → SafetyStandDownTriggered →
  WorkflowActionRequested` — across two modules on the real bus, neither referencing the other.

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **434 passed** (378 unit + 52 integration + 4 architecture), 0 failed. New coverage:
  rule resolution, unconfigured-trigger null, first-rule-wins, engine action-on-match, no-op without a rule,
  act-once idempotency, and the end-to-end Safety → Workflow capstone chain through the real bus.

### Sprint 34 — Business Module: HR (2026-07-20)

Added
- **`WorkerCertificationRecorded`**, **`ShiftStaffed`** and **`CertificationGapDetected`** in
  `FactoryOS.Contracts/Events` — the shared facts that a worker holds a certification with an expiry, that a
  worker was staffed on a certification-required shift, and that a valid required certification is missing or
  expired at the shift start.
- **`plugins/hr`** — the tenth business module, cross-checking two event streams:
  - `CertificationEvaluator` (pure) + `CertificationGap` — validity is checked against the **shift start**
    (event timestamps, no wall clock): a never-held certification is a `Missing` gap (config-gated), one that
    expired by the shift start is an `Expired` gap.
  - `ICertificationRegistry` / `InMemoryCertificationRegistry` + `WorkerKey` — a per-worker certification →
    expiry map, tenant-scoped, last-write-wins.
  - `IProcessedEventLog` / `InMemoryProcessedEventLog` — each staffing is checked once under at-least-once
    delivery.
  - `WorkerCertificationRecordedHandler` records certifications; `ShiftStaffedHandler` skips no-requirement
    shifts, dedupes, evaluates and emits `CertificationGapDetected` on a gap.
  - `HrPlugin` wires the registry, idempotency log and both handlers; `module.json` declares
    `consumes: [WorkerCertificationRecorded, ShiftStaffed]`, `emits: [CertificationGapDetected]`;
    `sample.config.json` sets `treatMissingAsGap` (true).

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **427 passed** (372 unit + 51 integration + 4 architecture), 0 failed. New coverage:
  valid/expired/missing evaluation and the missing-ignored policy, registry read/last-write-wins/unknown-key
  null, handler valid-no-gap, expired gap with expiry echoed, missing gap, no-requirement skip, redelivery
  raising one gap, and an end-to-end test through the real event bus.

### Sprint 33 — Business Module: Safety (2026-07-20)

Added
- **`SafetyIncidentReported`** and **`SafetyStandDownTriggered`** in `FactoryOS.Contracts/Events` — the shared
  facts that an incident (with a 1–5 severity) was reported at a site, and that a stand-down (work stoppage /
  review) is recommended, with the reason.
- **`plugins/safety`** — the ninth business module:
  - `SafetyEvaluator` (pure) + `SafetyDecision` — a single sufficiently severe incident triggers on
    `HighSeverity`; otherwise incidents accumulating past the frequency threshold trigger on `Frequency`.
    Severity takes precedence.
  - `IIncidentWindowStore` / `InMemoryIncidentWindowStore` + `SafetySiteKey` — a per-site bounded rolling
    incident count (saturating at the window size), tenant-scoped.
  - `IProcessedEventLog` / `InMemoryProcessedEventLog` — at-least-once redelivery is deduplicated by event id
    before an incident is folded into the window.
  - `SafetyIncidentReportedHandler` dedupes, folds, evaluates and emits `SafetyStandDownTriggered` on a trigger.
  - `SafetyPlugin` wires the window store, idempotency log and handler; `module.json` declares
    `consumes: [SafetyIncidentReported]`, `emits: [SafetyStandDownTriggered]`; `sample.config.json` sets the
    stand-down severity (4), frequency threshold (3) and window size (10).

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **414 passed** (360 unit + 50 integration + 4 architecture), 0 failed. New coverage:
  high-severity trigger, frequency trigger, severity-over-frequency precedence, minor-isolated no-trigger,
  window count saturation, site/tenant isolation, handler high-severity and frequency stand-downs, sites not
  combining, redelivery not double-counting, and an end-to-end test through the real event bus.

### Sprint 32 — Business Module: Carbon (2026-07-20)

Added
- **`CarbonEmissionCalculated`** in `FactoryOS.Contracts/Events` — the shared fact that a carbon-equivalent
  emission (per-reading and cumulative kg CO₂e) was derived from an energy consumption.
- **`plugins/carbon`** — the eighth business module, consuming Energy's `EnergyConsumptionRecorded`:
  - `EmissionFactorResolver` (pure) — resolves the emission factor per energy metric from configuration
    (case-insensitive), falling back to the configured default; the mapping is **data, not a branch**.
  - `CarbonCalculator` (pure) — emission = energy × factor.
  - `ICarbonLedger` / `InMemoryCarbonLedger` + `CarbonSourceKey` / `CarbonTotal` — a per-source cumulative
    CO₂e read-model, tenant-scoped.
  - `IProcessedEventLog` / `InMemoryProcessedEventLog` — because the total is accumulated, at-least-once
    redelivery is deduplicated by event id before accrual.
  - `EnergyConsumptionRecordedHandler` dedupes, resolves the factor, accrues and emits
    `CarbonEmissionCalculated`; metrics with no positive factor are ignored (default factor 0 = off).
  - `CarbonPlugin` wires the ledger, idempotency log and handler; `module.json` declares
    `consumes: [EnergyConsumptionRecorded]`, `emits: [CarbonEmissionCalculated]`; `sample.config.json`
    supplies emission factors as data (`ActivePower` 0.4, `NaturalGas` 2.02).
- **Third cross-module chain** proven end to end (`EnergyToCarbonChainTests`): an energy consumption becomes a
  carbon emission purely over the real event bus, with neither module referencing the other.

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **403 passed** (350 unit + 49 integration + 4 architecture), 0 failed. New coverage:
  explicit-factor resolution, case-insensitive lookup, default fallback, energy×factor emission, ledger
  accumulation and per-tenant/source isolation, handler compute+publish with cumulative total, growing
  cumulative across readings, unmapped-metric skip, redelivery not double-counting, and the end-to-end Energy →
  Carbon chain through the real bus.

### Sprint 31 — Business Module: Procurement (2026-07-20)

Added
- **`PurchaseRequisition`** in `FactoryOS.Contracts/StandardModel` — the canonical requisition entity (SKU,
  warehouse, requested quantity, status), natural-keyed by number — and **`PurchaseRequisitionRaised`** in
  `FactoryOS.Contracts/Events`.
- **`plugins/procurement`** — the seventh business module, consuming Warehouse's `LowStockDetected`:
  - `ReorderPolicy` (pure) — sizes the requisition to replenish up to `reorderPoint × ReorderMultiple`, never
    below the configured `MinimumOrderQuantity`.
  - `LowStockRequisitionFactory` (pure) — builds a `Draft` requisition; the number is derived from the
    consumed alert's event id, so re-processing the same alert yields the same number (idempotency basis).
  - `IPurchaseRequisitionStore` / `InMemoryPurchaseRequisitionStore` — per-tenant, `TryAdd` idempotent by
    number, so a redelivered alert neither raises a second requisition nor re-publishes.
  - `LowStockDetectedHandler` builds, stores and emits `PurchaseRequisitionRaised` (reason `LowStock`).
  - `ProcurementPlugin` wires the store and handler; `module.json` declares `consumes: [LowStockDetected]`,
    `emits: [PurchaseRequisitionRaised]`; `sample.config.json` sets the reorder policy.
- **Showpiece cross-module test** — `WarehouseToProcurementChainTests`: a stock movement crosses an item's
  reorder point in Warehouse and becomes a Procurement requisition **purely over the real event bus**, with
  both plugins registered side by side and neither referencing the other (Law 4, end to end).

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **392 passed** (340 unit + 48 integration + 4 architecture), 0 failed. New coverage:
  order-up-to sizing, minimum-order floor, deeper-shortfall sizing, handler requisition+publish, deterministic
  number from the source event, redelivery raising only one requisition, and the end-to-end Warehouse →
  Procurement chain through the real bus.

### Sprint 30 — Business Module: Warehouse (2026-07-20)

Added
- **`StockMovementRecorded`**, **`ItemReorderPointDefined`** and **`LowStockDetected`** in
  `FactoryOS.Contracts/Events` — the shared facts that stock moved for a SKU in a warehouse (a signed delta),
  that a reorder point was set for an item, and that an item crossed down to or below its reorder point.
- **`plugins/warehouse`** — the sixth business module:
  - `LowStockEvaluator` (pure) + `StockChange` — **edge-triggered** detection: an alert fires only on the
    downward crossing (from above the point to at/below it), not on every movement while already low; a
    non-positive reorder point disables detection.
  - `IStockLedger` / `InMemoryStockLedger` + `WarehouseStockKey` / `StockLevel` — a per-aggregate
    (tenant × warehouse × SKU) on-hand level and reorder point; `Apply` reports the level before and after so
    the crossing can be detected; reorder points are last-write-wins.
  - `IProcessedEventLog` / `InMemoryProcessedEventLog` — because a movement is an increment, at-least-once
    redelivery is deduplicated by event id before it is applied, so a replayed movement is never double-counted.
  - `StockMovementRecordedHandler` dedupes, applies and emits `LowStockDetected` on the crossing (per-item
    reorder point, else the configured `DefaultReorderPoint`); `ItemReorderPointDefinedHandler` records the
    threshold.
  - `WarehousePlugin` wires the ledger, idempotency log and both handlers; `module.json` declares
    `consumes: [StockMovementRecorded, ItemReorderPointDefined]`, `emits: [LowStockDetected]`;
    `sample.config.json` sets `defaultReorderPoint` (0 = off).

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **385 passed** (334 unit + 47 integration + 4 architecture), 0 failed. New coverage:
  downward-crossing detection, landing exactly on the point, no re-fire while already below, receipts staying
  above, disabled detection on a non-positive point, ledger signed-delta accumulation with before/after,
  last-write-wins reorder point, per-tenant isolation, handler crossing+publish, default-reorder-point
  fallback, silence without a point, redelivery not double-applying, and an end-to-end test through the real
  event bus.

### Sprint 29 — Business Module: Production (2026-07-20)

Added
- **`ProductionOrderReleased`**, **`ProductionCountReported`** and **`ProductionOrderCompleted`** in
  `FactoryOS.Contracts/Events` — the shared facts that an order was released with a target quantity, that a
  batch of units was produced against it (an increment, not a running total), and that the order reached its
  target.
- **`plugins/production`** — the fifth business module:
  - `IProductionOrderStore` / `InMemoryProductionOrderStore` + `ProductionOrderKey` / `ProductionOrderProgress`
    / `AccrualResult` — per-tenant order progress; registration is idempotent (a redelivered release preserves
    progress) and accrual reports `JustCompleted` exactly once, when an increment first carries the total to
    target. `AllowOverProduction` (config) either lets the total exceed target or caps it and locks the order.
  - `IProcessedEventLog` / `InMemoryProcessedEventLog` — because counts are increments, at-least-once
    redelivery is deduplicated by event id before accrual, so a replayed count is never double-counted.
  - `ProductionOrderReleasedHandler` registers the order; `ProductionCountReportedHandler` dedupes, accrues and
    emits `ProductionOrderCompleted` once on completion. Counts for an unreleased order are ignored (per-
    aggregate ordering releases before counting).
  - `ProductionPlugin` wires the store, idempotency log and both handlers; `module.json` declares
    `consumes: [ProductionOrderReleased, ProductionCountReported]`, `emits: [ProductionOrderCompleted]`;
    `sample.config.json` sets `allowOverProduction` (true).

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **371 passed** (321 unit + 46 integration + 4 architecture), 0 failed. New coverage:
  idempotent registration, not-found accrual on an unreleased order, fire-once completion, over-production
  accrual vs. cap-and-lock, per-tenant isolation, handler completion+publish with source-event id, single
  completion emit, ignored counts for unreleased orders, redelivery not double-counting, redelivered release
  preserving progress, and an end-to-end test through the real event bus.

### Sprint 28 — Business Module: Quality (2026-07-20)

Added
- **`QualityInspectionRecorded`** and **`QualityAlertRaised`** in `FactoryOS.Contracts/Events` — the shared
  facts that a batch of units was inspected on a line for a product (inspected/defective counts), and that a
  line-product's rolling defect rate breached its configured threshold.
- **`plugins/quality`** — the fourth business module:
  - `DefectRateEvaluator` (pure) + `DefectRateSnapshot` / `QualityEvaluation` — computes the window's defect
    rate = defective/inspected (guarded against an empty window) and flags a breach only when the rate is
    strictly above the threshold **and** enough units have been inspected, so a cold start never alerts on a
    single early defect.
  - `IDefectRateWindowStore` / `InMemoryDefectRateWindowStore` + `QualityLineKey` — a per-aggregate bounded
    rolling window (tenant × line × product); unlike a single-value baseline, the returned aggregate includes
    the just-folded inspection, so the current defective batch counts toward the rate.
  - `IProcessedEventLog` / `InMemoryProcessedEventLog` — module-local idempotency; at-least-once redelivery is
    deduplicated by event id before any inspection is folded into the window.
  - `QualityInspectionRecordedHandler : IEventHandler<QualityInspectionRecorded>` — dedupes, folds, evaluates
    and emits `QualityAlertRaised` on breach; an empty inspection (no units) is a no-op.
  - `QualityPlugin` wires the window store, idempotency log and handler; `module.json` declares
    `consumes: [QualityInspectionRecorded]`, `emits: [QualityAlertRaised]`; `sample.config.json` sets the
    defect-rate threshold (0.05), evidence floor (20 units) and window size (20).

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **359 passed** (310 unit + 45 integration + 4 architecture), 0 failed. New coverage:
  evidence-floor inertia, strictly-greater-than-threshold breach, at-threshold non-breach, empty-window
  safety, window accumulation and oldest-sample eviction, per-tenant/key isolation, handler breach+publish
  with source-event id, silence below the floor, empty-inspection no-op, redelivery suppression, and an
  end-to-end test through the real event bus.

### Sprint 27 — Business Module: OEE (2026-07-20)

Added
- **`ProductionPeriodReported`** and **`OeeCalculated`** in `FactoryOS.Contracts/Events` — the shared facts
  that a machine's production figures for a period are available, and that OEE was computed from them
  (Availability, Performance, Quality, OEE and whether it met target).
- **`plugins/oee`** — the third business module:
  - `OeeCalculator` (pure) + `OeeScore` — computes Availability = run/planned, Performance =
    (ideal×count)/run, Quality = good/total and their product; every factor guards its denominator (0, not an
    exception) and is clamped to `[0, 1]`, so a bad ideal cycle time can never report Performance above 100%.
  - `IOeeStore` / `InMemoryOeeStore` + `OeeSnapshot` — a per-tenant read-model keyed by machine and period;
    `TryAdd` is idempotent, so first-calculation-per-period wins and a redelivery is neither recomputed nor
    re-announced.
  - `ProductionPeriodReportedHandler : IEventHandler<ProductionPeriodReported>` — computes, stores and emits
    `OeeCalculated` with `MeetsTarget` from the configured `TargetOee`; skips periods with no planned time.
  - `OeePlugin` wires the store and handler; `module.json` declares `consumes: [ProductionPeriodReported]`,
    `emits: [OeeCalculated]`; `sample.config.json` sets the OEE target (0.85).

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **346 passed** (298 unit + 44 integration + 4 architecture), 0 failed. New coverage:
  each OEE factor and their product on a clean worked example (A 0.9 × P 0.8 × Q 0.75 = 0.54), performance
  clamping, zero-denominator safety, store dedup/period-retention/tenant isolation, handler compute+publish,
  meets-target flag, no-planned-time skip, redelivery suppression, and an end-to-end test through the real
  event bus.

### Sprint 26 — Business Module: Maintenance (work orders from energy spikes) (2026-07-20)

Changed
- **Promoted the cross-module integration events to shared vocabulary.** `EnergyConsumptionRecorded` and
  `EnergySpikeDetected` moved from `plugins/energy/Events` into `FactoryOS.Contracts/Events`. Events that cross
  a module boundary must live in the shared contract so consumers never reference the producing module (Law 4).
  The Energy plugin and its tests now reference the contract types; no behavioural change.

Added
- **`WorkOrderCreated`** in `FactoryOS.Contracts/Events` — the shared fact that a Standard Model `WorkOrder`
  was raised (carries the work order, a `Reason`, and the `SourceEventId` for traceability).
- **`plugins/maintenance`** — the second business module, consuming an event emitted by the first without any
  reference between them:
  - `SpikeWorkOrderFactory` (pure) — builds the corrective `WorkOrder` for a spike; the number is derived
    deterministically from the triggering event id, so re-processing the same spike yields the same number.
  - `IWorkOrderStore` / `InMemoryWorkOrderStore` — per-tenant work orders keyed by number; `TryAdd` is
    idempotent by number, which both persists and prevents duplicate creation.
  - `EnergySpikeDetectedHandler : IEventHandler<EnergySpikeDetected>` — raises the work order and emits
    `WorkOrderCreated`; a redelivered spike neither creates a second order nor re-announces.
  - `MaintenancePlugin` wires the store and handler; `module.json` declares `consumes: [EnergySpikeDetected]`,
    `emits: [WorkOrderCreated]`; `sample.config.json` tunes numbering and due date.

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **333 passed** (286 unit + 43 integration + 4 architecture), 0 failed. New coverage:
  deterministic work-order numbering and open-order shape, store dedup + tenant isolation, handler creation +
  announcement + duplicate suppression, and a **cross-module** end-to-end test where readings drive Energy to
  detect a spike which drives Maintenance to raise a work order — entirely through the real event bus, the two
  plugins composing with no reference between them.

### Sprint 25 — Business Module: Energy Monitoring (2026-07-20)

Added
- **`MeterReadingReceived`** in `FactoryOS.Contracts/Events` — the shared canonical fact that a Standard Model
  `MeterReading` was observed on the bus. Any producer publishes it; any module consumes it — modules never
  reference one another.
- **`plugins/energy`** — the first business module, a self-contained event-driven plugin:
  - `EnergySpikeEvaluator` (pure) + `SpikeEvaluation` — decides whether a reading exceeds its rolling baseline
    by the configured threshold; inert until `MinimumSamples` history exists and when the baseline is
    non-positive, so cold starts never fire false spikes.
  - `IEnergyBaselineStore` / `InMemoryEnergyBaselineStore` — a bounded rolling window per meter aggregate
    (`EnergyMeterKey` = tenant + meter + metric); per-aggregate, thread-safe, Redis-swappable behind the
    interface.
  - `IProcessedEventLog` / `InMemoryProcessedEventLog` — deduplication by event id, realizing the idempotent-
    consumer invariant against at-least-once delivery.
  - `MeterReadingReceivedHandler : IEventHandler<MeterReadingReceived>` — records consumption for every reading
    and emits a spike when the baseline is exceeded, deduplicating before folding into the baseline.
  - Emits `EnergyConsumptionRecorded` and `EnergySpikeDetected`; `EnergyPlugin` wires the store, log and
    handler. `module.json` declares `consumes`/`emits`; `sample.config.json` tunes threshold and window.
    Installing/removing the folder adds/removes the module with zero core changes.

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **325 passed** (279 unit + 42 integration + 4 architecture), 0 failed. New coverage:
  spike thresholds (below-min-samples, zero baseline, within/at/above threshold, drops), baseline rolling
  window (prior-snapshot semantics, bounded eviction, aggregate isolation), handler behaviour (consumption
  per reading, spike after baseline, duplicate ignored, duplicate does not pollute the baseline), and an
  end-to-end test driving readings through the **real in-process event bus** and capturing the energy events
  published back.

### Sprint 24 — Integration: Identity Connectors (LDAP / Active Directory / Entra ID) (2026-07-20)

Added
- **Standard Model identity entities** in `FactoryOS.Contracts/StandardModel` — `DirectoryUser` (natural key
  `Username`; `DisplayName`, `Email`, `Enabled`) and `DirectoryGroup` (natural key `GroupName`; `DisplayName`,
  `Description`). LDAP `inetOrgPerson`, AD `user` and Entra `user` all normalize into these single entities.
  `StandardEntityBinder` now binds both (with a boolean `Enabled` reader).
- **`connectors/ldap`** — the `LdapConnector` reads users and groups from a generic LDAP directory through an
  injected `ILdapClient` transport (library-agnostic, fully offline-testable), tagging raw records `USERS` /
  `GROUPS`. `mapping.json` normalizes `uid`/`cn`/`mail` → `DirectoryUser`/`DirectoryGroup`.
- **`connectors/activedirectory`** — the `ActiveDirectoryConnector` reads through an injected `IActiveDirectory`
  transport and derives account `enabled` from the `userAccountControl` `ACCOUNTDISABLE` bit, surfacing it as a
  plain field so the mapping stays declarative. `mapping.json` normalizes `sAMAccountName`/`displayName`.
- **`connectors/entraid`** — the `EntraIdConnector` reads Microsoft Graph (`/v1.0/users`, `/v1.0/groups`) over
  an `HttpClient` with a bearer token, normalizing `userPrincipalName`/`accountEnabled` into the Standard Model.
- Each connector ships `connector.json`, `mapping.json` (mapping is **data, not code**) and a
  `sample.config.json` using `${secret:…}` placeholders; all three declare `provides: [DirectoryUser,
  DirectoryGroup]`. Registered in `FactoryOS.slnx` and referenced by the integration-test project. Every
  connector is independently installable — no core change adds them.

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **312 passed** (267 unit + 41 integration + 4 architecture), 0 failed. New coverage:
  binder DirectoryUser/DirectoryGroup (boolean + defaulted `Enabled`), and end-to-end ingest→normalize→bind
  for all three connectors — LDAP (trim/lower transforms, constant `Enabled`), AD (`userAccountControl`→
  enabled derivation, disabled account), and Entra (bearer auth, users+groups routing, `accountEnabled`,
  null email).

### Sprint 23 — AI Platform: Agent Framework (2026-07-20)

Added
- **Agent contracts** in `FactoryOS.Contracts/Ai` — `AgentDefinition` (the manifest: key, name, version,
  description, system prompt, logical chat model and optional `AgentGrounding`), `AgentRequest` (tenant, agent
  key, input, optional prompt variables) and `AgentResponse` (output, model, grounding chunks). Per the
  Constitution an agent is data + a shared runtime — the manifest **is** the difference between agents.
- **Agent framework** in `FactoryOS.Ai/Agents`:
  - `IAgentCatalog` / `InMemoryAgentCatalog` — the manifest registry; agents are discovered by key (contracts
    over names), highest version wins.
  - `IAgentRuntime` / `AgentRuntime` — the single runtime that executes **any** agent: resolve manifest →
    render the system prompt (strict `{{variable}}` binding via the Prompt Engine) → optionally ground the
    task in the tenant's knowledge base (RAG) → generate through the LLM Gateway. No agent-specific code.
    `Ai.Agent.UnknownAgent` when the key is absent; render/retrieval/generation failures propagate.
  - `AddAgentFramework()` ensures the knowledge base and prompt engine, registers the catalog and runtime;
    wired into `AddInfrastructure`.
- **`src/FactoryOS.Ai/sample.agents.json`** — a sample manifest pack: an ungrounded triage agent and a
  knowledge-grounded advisor agent, differing only by manifest.

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **303 passed** (264 unit + 35 integration + 4 architecture), 0 failed. New coverage:
  catalog versioning/enumeration/unknown-key, ungrounded run from manifest, grounded run injecting RAG
  context and returning citations, system-prompt variable binding and strict unbound-variable failure, and
  unknown-agent failure.

### Sprint 22 — AI Platform: Company Brain (2026-07-20)

Added
- **Company Brain contracts** in `FactoryOS.Contracts/Ai` — `BrainQuestion` (tenant, question, logical chat +
  embedding model keys, `TopK`), `BrainAnswer` (answer text, `Model`, `Citations`) and `BrainCitation`
  (source, chunk id, similarity score). Answers are grounded and attributable.
- **Company Brain** in `FactoryOS.Ai/Brain`:
  - `ICompanyBrain` / `CompanyBrain` — the tenant-aware grounded Q&A facade. Pipeline: retrieve grounding
    (RAG) → render context (`RagContext`) → compose the grounded-answer prompt (Prompt Engine) → generate
    (LLM Gateway) → return the answer with the chunks it used as citations. Every step is tenant-scoped and
    any failure short-circuits.
  - `BrainPrompts` — the built-in `company-brain.answer` template, versioned and kept as data. Its system
    prompt is the RAG guardrail: answer **only** from the numbered context, admit when it is insufficient,
    cite `[n]` markers.
  - `AddCompanyBrain()` ensures the knowledge base and prompt engine, seeds the built-in template into the
    catalog (idempotent — highest version wins) and registers the brain; wired into `AddInfrastructure`.
- **`sample.prompts.json`** — adds the `company-brain.answer` template to the sample prompt pack.

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **295 passed** (256 unit + 35 integration + 4 architecture), 0 failed. New coverage:
  grounded answering with citations, context injection into the exact prompt the model sees, empty-retrieval
  answering with no citations, and tenant isolation across both retrieval and generation (another tenant's
  chunk never reaches the model).

### Sprint 21 — AI Platform: RAG & Knowledge Base (2026-07-20)

Added
- **Knowledge contracts** in `FactoryOS.Contracts/Ai` — `KnowledgeDocument` (a raw, tenant-scoped document to
  ingest), `KnowledgeChunk` (one retrievable, embedded unit: `Id`, `Tenant`, `Source`, `Ordinal`, `Text`) and
  `ScoredChunk` (a retrieval hit with its cosine `Score`). Every unit carries its tenant — knowledge never
  crosses tenants.
- **Knowledge base** in `FactoryOS.Ai/Knowledge`:
  - `TextChunker` — pure, deterministic, word-aligned chunking with configurable size and overlap; never
    splits a word, preserves cross-boundary context.
  - `IKnowledgeStore` / `InMemoryKnowledgeStore` — a **tenant-scoped** vector store; every operation takes the
    tenant explicitly and each tenant has its own bucket, so no query reaches another's data. Brute-force
    cosine ranking (via `VectorMath`), upsert-by-id, `Ai.Knowledge.{TenantMismatch,InvalidTopK}` guards.
    Replaceable by pgvector/an external store behind the interface.
  - `IKnowledgeIndexer` / `KnowledgeIndexer` — chunk → batch-embed (through the `IEmbeddingGateway`) → upsert;
    stamps the document's tenant onto every chunk; `Ai.Knowledge.EmbeddingCountMismatch` on provider drift.
  - `IKnowledgeRetriever` / `KnowledgeRetriever` — embed the query and search the tenant namespace for the
    nearest chunks (the "R" in RAG).
  - `RagContext` — renders retrieved chunks into a numbered, source-attributed grounding block for injection
    into a prompt (the bridge to augmented generation via the Prompt Engine / LLM Gateway).
  - `AddKnowledgeBase()` registers the store, indexer and retriever; wired into `AddInfrastructure`.
- **`src/FactoryOS.Ai/sample.knowledge.json`** — a sample tenant knowledge pack (pump-maintenance and
  boiler-safety documents) with embedding model and retrieval settings.

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **291 passed** (252 unit + 35 integration + 4 architecture), 0 failed. New coverage:
  chunking (empty, single, size-budgeted split, whole-word overlap), store ranking/top-K/tenant isolation/
  upsert-replace/tenant-mismatch/invalid-top-K, and an offline index→retrieve→context RAG pipeline (relevance
  ranking, tenant scoping, empty-document, provider vector-count drift, and grounding-block formatting).

### Sprint 20 — AI Platform: Embeddings (2026-07-20)

Added
- **`EmbeddingRequest` / `EmbeddingResponse`** in `FactoryOS.Contracts/Ai` — the canonical, provider-agnostic
  embedding contract. A request carries its `Tenant`, a logical `Model` key and a batch of `Inputs`; a
  response returns one vector per input (`IReadOnlyList<IReadOnlyList<float>>`), reported prompt tokens and a
  computed `Dimensions`. Providers normalize their own dialect into this shape — the AI equivalent of the
  Standard Model.
- **Embedding providers** in `FactoryOS.Ai/Providers`:
  - `IEmbeddingProvider` — a single embedding backend behind the gateway.
  - `OpenAiEmbeddingProvider` — POSTs to `/v1/embeddings` with bearer auth and an `input` array, normalizing
    the `data[].embedding` / `usage.prompt_tokens` dialect. Errors: `Ai.Embedding.OpenAi.{HttpError,Transport,EmptyResponse,InvalidResponse}`.
  - `OllamaEmbeddingProvider` — POSTs to `/api/embed` (batch), normalizing the `embeddings[]` /
    `prompt_eval_count` dialect. Errors: `Ai.Embedding.Ollama.*`.
- **Embedding Gateway** in `FactoryOS.Ai/Gateway` — `IEmbeddingGateway` / `EmbeddingGateway` route a logical
  model key to a configured provider and rewrite to the upstream model name; unknown model/provider fail fast
  (`Ai.Embedding.UnknownModel` / `Ai.Embedding.UnknownProvider`). New embedding models are configuration, not code.
- **`VectorMath`** in `FactoryOS.Ai/Vectors` — `CosineSimilarity`, `Dot` and `Magnitude` over embedding
  vectors, the retrieval math the RAG sprint builds on. Undefined cases are typed failures
  (`Ai.Vector.DimensionMismatch`, `Ai.Vector.ZeroMagnitude`).
- **`EmbeddingGatewayOptions`** (`Ai:Embeddings`) — the logical→upstream routing table; `AddEmbeddingGateway()`
  wires providers and gateway and is registered in `AddInfrastructure`.
- **`src/FactoryOS.Ai/sample.config.json`** — an `Ai:Embeddings` section (`embed`, `embed-large`,
  `embed-local`) alongside the existing LLM routing.

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **277 passed** (238 unit + 35 integration + 4 architecture), 0 failed. New coverage:
  OpenAI request shaping (endpoint, bearer header, input array) and vector/usage/dimension normalization,
  Ollama `/api/embed` normalization and empty-response failure, gateway routing + upstream rewrite + unknown
  model/provider failures, and cosine-similarity/dot/magnitude math including orthogonal, opposite,
  zero-vector and dimension-mismatch cases.

### Sprint 19 — AI Platform: Prompt Engine (2026-07-20)

Added
- **`PromptTemplate`** in `FactoryOS.Contracts/Ai` — a named, versioned template with optional system and
  required user bodies carrying `{{variable}}` placeholders. Prompts are data, not code.
- **Prompt engine** in `FactoryOS.Ai/Prompts`:
  - `IPromptRenderer` / `PromptRenderer` — strict `{{name}}` substitution via a `GeneratedRegex`; an
    unbound placeholder is a typed failure (`Ai.Prompt.MissingVariable`), never a silent empty string.
  - `IPromptCatalog` / `InMemoryPromptCatalog` — a thread-safe registry that resolves the highest
    registered version for a key.
  - `IPromptComposer` / `PromptComposer` — catalog lookup + render + assembly into canonical
    `ChatMessage`s (system when present, then user), ready for the LLM Gateway. `Ai.Prompt.UnknownTemplate`
    when the key is absent; render failures propagate.
  - `AddPromptEngine()` registers the three services and is wired into `AddInfrastructure`.
- **`src/FactoryOS.Ai/sample.prompts.json`** — a sample prompt pack (maintenance work-order summary, energy
  spike explanation) showing versioned templates with placeholders.

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **262 passed** (223 unit + 35 integration + 4 architecture), 0 failed. New coverage:
  placeholder substitution and whitespace-tolerant names, strict missing-variable failure, version
  resolution in the catalog, and composition into system/user messages (with and without a system body,
  unknown-template and unbound-variable failures).

### Sprint 18 — AI Platform: LLM Gateway (2026-07-20)

Added
- **Canonical AI model** in `FactoryOS.Contracts/Ai` — `ChatRole`, `ChatMessage`, `ChatCompletionRequest`
  (carries its `Tenant` and a *logical* model key) and `ChatCompletionResponse` (model, content, finish
  reason, token usage). This is the Standard Model applied to AI: providers normalize their own dialect
  into it.
- **`FactoryOS.Ai` project — the LLM Gateway**, the single vendor-agnostic door to language models (the AI
  analogue of the Connector layer). AI is called strictly over HTTP, never in-process:
  - `ILlmProvider` with two implementations: `OpenAiCompatibleProvider` (`/v1/chat/completions`, Bearer
    auth — OpenAI, Azure OpenAI, vLLM, LM Studio) and `OllamaProvider` (`/api/chat`, non-streaming). Each
    builds its request with `System.Text.Json.Nodes` and normalizes the response with `JsonDocument`;
    transport and backend errors become typed `Result` failures (`Ai.Llm.*`).
  - `ILlmGateway` / `LlmGateway` routes a logical model key (`fast`, `reasoning`, `local`) to a provider
    and rewrites it to the concrete upstream model — new models are configuration, not code. Unknown
    models/providers fail fast (`Ai.Llm.UnknownModel` / `Ai.Llm.UnknownProvider`).
  - `AddLlmGateway(configuration)` binds `LlmGatewayOptions` + provider options, registers both providers
    as typed `HttpClient`s (base address from options), and is wired into `AddInfrastructure`.
- **`src/FactoryOS.Ai/sample.config.json`** — an `appsettings` fragment declaring the model routing table
  and provider endpoints with a secret placeholder for the API key.

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **254 passed** (215 unit + 35 integration + 4 architecture), 0 failed. New coverage:
  OpenAI request shaping (endpoint, Bearer header, message/role/param serialization) and response
  normalization, non-success → failure mapping, key-less requests; Ollama request/response shaping; and
  gateway routing (upstream rewrite, provider selection, unknown-model/unknown-provider failures) — all
  offline via a stub `HttpMessageHandler`.

### Sprint 17 — Edge Gateway: Modbus & Siemens S7 Decoders (2026-07-20)

Added
- **Fieldbus decoders** in `edge/FactoryOS.Edge`, extending the pure decode-frame pattern to industrial
  PLC buses (same contract as MQTT/OPC-UA — decode only, the IoT hub calibrates):
  - **Modbus**: `ModbusRegisterReading`, `ModbusRegisterType` (holding/input), `ModbusDataType`
    (Int16/UInt16/Int32/UInt32/Float32) with `ModbusWordOrder` (big/little-endian word swap for 32-bit
    values), `ModbusRegisterMap` (binds `(registerType, address)` to a device tag + data type — mapping
    as data) and `ModbusTelemetryDecoder` (assembles 16/32-bit values from register words honouring word
    order; `Edge.Modbus.UnmappedRegister` / `Edge.Modbus.InsufficientRegisters` failures).
  - **Siemens S7**: `S7Variable` (area + DB number + byte offset + data type), `S7Area`
    (DataBlock/Input/Output/Memory), `S7DataType` (Int/Word/DInt/DWord/Real), `S7VariableReading`,
    `S7VariableMap` and `S7TelemetryDecoder` (interprets big-endian bytes via `BinaryPrimitives`;
    `Edge.S7.UnmappedVariable` / `Edge.S7.InsufficientData` failures).
- **`edge/FactoryOS.Edge/sample.config.json`** — an edge gateway config declaring all four source
  protocols (MQTT, OPC-UA, Modbus, S7) with their device/tag maps and secret placeholders.

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors** (CA1720 suppressed with
  justification on the two data-type enums whose members are canonical wire type names).
- `dotnet test` → **246 passed** (207 unit + 35 integration + 4 architecture), 0 failed. New coverage:
  Modbus signed/unsigned 16- and 32-bit decoding, word-order swap and Float32; S7 Int/DInt/Real
  big-endian decoding; unmapped/insufficient-data failures; and a full fieldbus → hub path where Modbus
  and S7 reads normalize into calibrated `MeterReading`s (Wh→kWh scaling, °C pass-through).

### Sprint 16 — Edge Gateway: MQTT & OPC-UA Decoders (2026-07-20)

Added
- **Edge Gateway** in the new `edge/FactoryOS.Edge` project — protocol decoders that turn raw frames into
  Standard-Model-bound `TelemetrySample`s (the edge never calibrates; that is the IoT hub's job):
  - **MQTT**: `MqttMessage`, `MqttTopicTemplate` (slash-segmented template with `{device}`/`{tag}`
    placeholders and `+` wildcards) and `MqttTelemetryDecoder` (extracts device/tag from the topic,
    parses the payload as an invariant decimal).
  - **OPC-UA**: `OpcUaNodeReading`, `OpcUaNodeMap` (binds node ids to device tags — mapping as data) and
    `OpcUaTelemetryDecoder`.
- References only `FactoryOS.Contracts` and `FactoryOS.Domain`; broker/client wiring is deferred so the
  decode logic stays pure and fully testable offline.

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **234 passed** (196 unit + 34 integration + 4 architecture), 0 failed. New coverage:
  MQTT topic/template matching (literals, wildcards, placeholders) and payload parsing; OPC-UA node
  resolution; and a full edge → hub path where MQTT and OPC-UA frames decode and then normalize into
  calibrated `MeterReading`s (scale and offset applied).

### Sprint 15 — IoT Hub: Device Registry & Telemetry Model (2026-07-20)

Added
- **IoT contracts** in `FactoryOS.Contracts.Iot`: `Device` (tenant-scoped, with a tag catalogue),
  `DeviceTag` (raw channel → canonical metric/unit with linear `Scale`/`Offset` calibration) and
  `TelemetrySample` (raw device reading).
- **IoT Hub** in the new `FactoryOS.Iot` project:
  - `IDeviceRegistry` / `InMemoryDeviceRegistry` — the tenant-scoped authority on registered devices and
    their tags.
  - `ITelemetryNormalizer` / `TelemetryNormalizer` — resolves the device and tag, applies the tag
    calibration (`value · Scale + Offset`) and produces a Standard Model `MeterReading`; rejects unknown
    or disabled devices and unknown tags.
  - `ITelemetryIngestor` / `TelemetryIngestor` — normalizes a batch, collecting readings and per-sample
    errors so one bad sample never fails the batch.
  - `AddIotHub`, wired into `AddCore`.

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **227 passed** (190 unit + 33 integration + 4 architecture), 0 failed. New coverage:
  tenant-scoped registry registration/lookup/replacement, telemetry calibration to `MeterReading`,
  rejection of unknown/disabled devices and unknown tags, and batch ingestion with mixed success/errors.

### Sprint 14 — SAP & Oracle Connectors (2026-07-20)

Added
- **SAP connector** (`connectors/sap`, `FactoryOS.Connectors.Sap`): reads the material master (`MARA`)
  joined to its language-filtered description (`MAKT`, parameterized `SPRAS`) and unrestricted stock
  summed across storage locations (`MARD.LABST`), normalized to `InventoryItem`.
- **Oracle connector** (`connectors/oracle`, `FactoryOS.Connectors.Oracle`): reads the Oracle EBS item
  master (`MTL_SYSTEM_ITEMS_B`) with on-hand summed from `MTL_ONHAND_QUANTITIES`, normalized to
  `InventoryItem`.
- Both are provider-agnostic (injected `Func<DbConnection>`), reference only `FactoryOS.Contracts`, and
  ship `connector.json`, `mapping.json` and `sample.config.json`. The complete ERP connector set is now
  Logo, Netsis, Mikro, SAP and Oracle — plus the generic SQL, REST and CSV sources.

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **219 passed** (182 unit + 33 integration + 4 architecture), 0 failed. New coverage:
  SAP material read with a parameterized language filter and multi-location stock summation; Oracle EBS
  item read with summed on-hand — both binding to `InventoryItem`.

### Sprint 13 — Netsis & Mikro Connectors (2026-07-20)

Added
- **Netsis connector** (`connectors/netsis`, `FactoryOS.Connectors.Netsis`): reads the item master
  (`TBLSTSABIT`) with on-hand aggregated from stock movements (`TBLSTHAR`, signed by `STHAR_GCKOD`
  entry/exit), normalized to `InventoryItem`.
- **Mikro connector** (`connectors/mikro`, `FactoryOS.Connectors.Mikro`): reads the stock master
  (`STOKLAR`) with on-hand aggregated from `STOK_HAREKETLERI` (signed by `sth_tip`), normalized to
  `InventoryItem`.
- Both are provider-agnostic (injected `Func<DbConnection>`), reference only `FactoryOS.Contracts`, and
  ship `connector.json`, `mapping.json` and `sample.config.json`.

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **217 passed** (182 unit + 31 integration + 4 architecture), 0 failed. New coverage:
  Netsis and Mikro reads over SQLite databases shaped like each ERP, aggregating signed movements into an
  on-hand balance and binding to `InventoryItem`.

### Sprint 12 — Logo Connector (2026-07-20)

Added
- **Logo connector** (`connectors/logo`, `FactoryOS.Connectors.Logo`) — the first ERP-specific driver:
  - `LogoObjectNames` encodes Logo's firm/period table-naming convention (`LG_{firm:000}_ITEMS`,
    `LG_{firm:000}_{period:00}_STINVTOT`) — knowledge the generic SQL connector lacks.
  - `LogoConnector` reads the firm's item master joined to the period's stock totals (on-hand via
    `COALESCE`), emitting one raw `SourceRecord` per item tagged `ITEMS`; provider-agnostic via an
    injected `Func<DbConnection>`.
  - `mapping.json` normalizes Logo's dialect (`CODE`, `NAME`, `ONHAND`, `UNIT`) into `InventoryItem`;
    ships `connector.json` and `sample.config.json` (firm/period + secret-referencing connection).
  - References only `FactoryOS.Contracts` — independently installable.

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **215 passed** (182 unit + 29 integration + 4 architecture), 0 failed. New coverage:
  Logo table-name convention, an items⋈stock-totals read over a SQLite database shaped like Logo
  (including a stock-less item resolving to quantity 0), and an end-to-end pipeline run binding to
  `InventoryItem`.

### Sprint 11 — Generic Connectors: CSV, SQL, REST (2026-07-20)

Added
- **CSV connector** (`connectors/csv`, `FactoryOS.Connectors.Csv`): `CsvConnector` streams a delimited
  file into one `SourceRecord` per row, with a minimal RFC 4180-style `CsvRowParser` (quoted fields,
  escaped quotes) and strongly-typed `CsvConnectorOptions` (path, delimiter, header). Field names come
  from the header row, or are positional when absent.
- **SQL connector** (`connectors/sql`, `FactoryOS.Connectors.Sql`): `SqlConnector` runs a query and
  yields one `SourceRecord` per result row, keyed by column name. It is provider-agnostic via
  `IDbConnectionFactory` (any ADO.NET provider — PostgreSQL, SQL Server, SQLite …).
- **REST connector** (`connectors/rest`, `FactoryOS.Connectors.Rest`): `RestConnector` GETs a JSON
  resource and yields one `SourceRecord` per object in a configurable array path (`data.items`, or the
  root array), converting scalar JSON values to CLR primitives.
- Each connector is an independently installable plugin referencing only `FactoryOS.Contracts`, and
  ships `connector.json`, a `mapping.json` (source → `InventoryItem`) and a `sample.config.json`.

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **210 passed** (182 unit + 24 integration + 4 architecture), 0 failed. New coverage:
  CSV quote/escape parsing and header-based reads; SQL reads over a shared in-memory SQLite database;
  REST reads over a stubbed HTTP handler (nested and root array paths); and each connector driving the
  ingestion pipeline end-to-end through its shipped `mapping.json` to a bound `InventoryItem`.

### Sprint 10 — Connector Framework + Standard Model (2026-07-20)

Added
- **Standard Model** — the only shared language on the bus — in `FactoryOS.Contracts.StandardModel`:
  `IStandardEntity` (tenant, entity type, natural key) plus the canonical entities `InventoryItem`,
  `MeterReading`, `Asset` and `WorkOrder`. Vendor dialects (`LogoStock`, `SAP.Material`,
  `Netsis.ItemCard`) all normalize into these types.
- **Connector contracts** in `FactoryOS.Contracts.Connectors`: `IConnector` (the only door to the
  outside; reads raw `SourceRecord`s), `ConnectorReadContext` (always tenant-scoped), `NormalizedRecord`
  (the Standard Model wire form), `ConnectorManifest` (`connector.json`), and the **mapping-as-data**
  models `MappingManifest` / `EntityMapping` / `FieldMapping`.
- **Connector framework** in the new `FactoryOS.Connectors` project — the normalize/dedup pipeline:
  - `IValueTransformer` / `ValueTransformer` — culture-invariant, named field transforms
    (`trim`, `upper`, `lower`, `decimal`, `int`, `bool`, `datetime`); identity when unnamed.
  - `IRecordNormalizer` / `RecordNormalizer` — applies a mapping manifest to a source record: resolves
    each field (constant, source or default), transforms it, enforces required fields and builds the
    natural key. This is where dialects become the Standard Model.
  - `IRecordDeduplicator` / `RecordDeduplicator` — collapses records by (tenant, entity type, natural
    key), last-write-wins, for at-least-once delivery; preserves per-aggregate order.
  - `IIngestionPipeline` / `IngestionPipeline` — reads a connector, normalizes (collecting per-record
    errors rather than throwing) and deduplicates, returning an `IngestionResult` with counts and errors.
  - `IStandardEntityBinder` / `StandardEntityBinder` — materializes a typed Standard Model entity from a
    normalized record, closing the loop (a Logo row and a SAP row bind to the same `InventoryItem`).
  - `ConnectorManifestReader` and `MappingManifestReader` (JSON scalars converted to CLR primitives).
  - `AddConnectorFramework`, wired into `AddCore`.
- **Sample connector** `connectors/sample/` (`FactoryOS.Connectors.Sample`, references only Contracts):
  `SampleLogoConnector` yields Logo `LG_STLINE` rows (including a repeated SKU) with `connector.json`,
  `mapping.json` (Logo → `InventoryItem`) and `sample.config.json`.

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **200 passed** (182 unit + 14 integration + 4 architecture), 0 failed. New coverage:
  every transform, normalizer field/constant/default/required/natural-key/error paths, deduplication,
  typed binding, both manifest readers, and an end-to-end pipeline run over the real sample connector and
  its `mapping.json` (read → normalize → dedup → bind to `InventoryItem`).

### Sprint 9 — Module Loader + API Gateway (2026-07-20)

Added
- **Module loader** in `FactoryOS.Plugin`: `IModuleLoader` / `ModuleLoader` activates a plugin from its
  manifest alone — loading the entry assembly through a collectible `PluginLoadContext`, resolving the
  declared (or single) `IPlugin` entry type, and verifying its key matches the manifest.
- `AddPluginModules(pluginsRoot)` — the modular-monolith bootstrap: it discovers every plugin folder,
  loads and activates each one, lets it contribute its services, and registers a fully configured
  `IPluginHost`. A missing root or a failing plugin never aborts start-up; the plugin is marked failed
  and skipped.
- **API Gateway** in the new `FactoryOS.Gateway` project:
  - `IModuleApi` — the contract a plugin implements to contribute HTTP endpoints; the gateway mounts
    each active module's endpoints under a reserved prefix, **`/m/<key>/*`**, so modules never collide.
  - `MapModuleGateway` — dynamic route mounting for every active module plus `GET /modules` (inventory
    with lifecycle state) and `GET /modules/ui` (the **UI lazy-load registry**). Disabled, failed and
    unknown modules are never mounted, so the routable surface reflects exactly the active plugin set.
  - `IModuleUiCatalogProvider` / `ModuleUiCatalogProvider` — aggregates the UI screens declared in each
    active module's manifest into a deterministically ordered catalog.
  - `PluginLifecycleHostedService` — starts every configured plugin on application start and stops them,
    in reverse order, on shutdown.
- **UI screens as manifest data**: `PluginUiScreen` and `PluginManifest.Ui` let a plugin declare its
  screens (id, title, route, lazy-loaded component, icon, permission, nav section, order) as data in
  `module.json`; the manifest reader parses and validates them. The sample plugin now declares two.
- The API host wires it together: `AddPluginModules` + `AddModuleGateway` and `app.MapModuleGateway()`.

Changed
- `PluginLoadContext` now unifies contract and framework assemblies already loaded by the host with the
  default context (even when a plugin ships a private copy), guaranteeing a single `IPlugin`/event/domain
  type identity across the core–plugin boundary — the seam the modular monolith depends on.

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **172 passed** (156 unit + 12 integration + 4 architecture), 0 failed. New coverage:
  module loading against the real sample assembly, manifest UI parsing, UI catalog aggregation/ordering,
  and gateway endpoints over an in-memory `TestServer` (mounting, `/modules`, `/modules/ui`, and the
  non-mounting of disabled/unknown modules).

### Sprint 8 — Persistence (2026-07-19)

Added
- Opt-in persistence contracts in `FactoryOS.Domain.Abstractions`: `IAuditableEntity`, `ISoftDeletable`
  and `IConcurrencyStamped` — entities declare the cross-cutting behavior they want.
- EF Core (PostgreSQL) persistence in `FactoryOS.Persistence`:
  - `FactoryOsDbContext` — the base context that applies platform conventions to the whole model:
    **tenant schema isolation** (`HasDefaultSchema` on PostgreSQL), **soft-delete query filters**
    (auto-built `!IsDeleted` predicates) and **optimistic-concurrency tokens**.
  - `AuditingSaveChangesInterceptor` — fills audit metadata, converts hard deletes of soft-deletable
    entities into flag updates, and stamps a fresh concurrency token on every write; driven by an
    injected clock and `ICurrentActorProvider` (`SystemActorProvider` default).
  - `ITenantSchemaProvider` / `FixedTenantSchemaProvider` for schema-per-tenant resolution.
  - `EfRepository<TAggregate, TId>` (implements the Domain repository contract), `EfUnitOfWork`
    (implements `IUnitOfWork`), and `ITransactionalExecutor` / `EfTransactionalExecutor`
    (commit-on-success / rollback-on-failure, reusing an ambient transaction).
  - `IDatabaseInitializer` / `RelationalDatabaseInitializer` — creates the tenant schema and applies
    migrations on PostgreSQL, or creates the model directly on schema-less providers (validated schema
    identifier).
  - `AddPersistence` registers the cross-cutting services. Packages: `Microsoft.EntityFrameworkCore`
    10.0.10, `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3.
- 5 **integration tests** (`FactoryOS.IntegrationTests`) on **SQLite in-memory**, exercising the real
  EF Core pipeline: CRUD via repository + unit of work, audit stamping, soft-delete filtering
  (with `IgnoreQueryFilters` verification), optimistic-concurrency rejection of a stale write, and
  transactional rollback.

Changed
- Aligned all `Microsoft.Extensions.*` package references to `10.0.10` (the EF Core 10 wave) to avoid
  NU1605 downgrade conflicts. Pinned `SQLitePCLRaw.bundle_e_sqlite3` `3.0.4` in the integration-test
  project to clear a transitive advisory (NU1903).

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **156 passed** (147 unit + 4 architecture + 5 integration), 0 failed.

### Sprint 7 — Identity (2026-07-19)

Added
- Identity domain aggregates in `FactoryOS.Identity.Domain`: `Tenant`, `Organization`, `Role`
  (grant/revoke/`Grants`) and `User` (role assignment, activation, password change), built on the
  Domain DDD primitives.
- Authorization in `FactoryOS.Identity.Authorization`: `Permission` value object (`resource.action`
  with `*` and `resource.*` wildcards and `Grants` semantics), `AuthorizationPolicy` (require-all /
  require-any) and `IPermissionAuthorizer` / `PermissionAuthorizer`.
- Credentials: `IPasswordHasher` / `Pbkdf2PasswordHasher` (PBKDF2-SHA256, 100k iterations, per-hash
  salt & parameters, constant-time verify).
- Claims: `FactoryClaimTypes` and `ClaimsFactory` (subject/tenant/org/username/email/role/permission),
  keeping tenant always in scope.
- Tokens: `JwtOptions`, `IAccessTokenService` / `JwtAccessTokenService` (HS256 JWTs, clock-driven
  lifetimes, full validation), and refresh tokens — `RefreshToken`, `IRefreshTokenStore` /
  `InMemoryRefreshTokenStore`, `IRefreshTokenService` / `RefreshTokenService` (issue/validate/rotate/
  revoke).
- Persistence abstractions `IUserStore` / `IRoleStore` with in-memory implementations.
- Authentication: `IAuthenticator` / `Authenticator` — verifies credentials, resolves effective
  roles & permissions, issues access + refresh tokens, and rotates on refresh (indistinguishable
  credential failures).
- DI: `AddIdentityModule` binds `JwtOptions` and registers the full identity pipeline. Packages:
  `System.IdentityModel.Tokens.Jwt` / `Microsoft.IdentityModel.Tokens` 8.3.0,
  `Microsoft.Extensions.Options.ConfigurationExtensions` 10.0.0.
- 39 unit tests across permissions, password hashing, aggregates, JWT round-trip/expiry/tamper/key,
  refresh-token lifecycle, permission authorization/policies, claims, and end-to-end authentication.

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **151 passed** (147 unit + 4 architecture), 0 failed.

### Sprint 6 — Configuration (2026-07-19)

Added
- Strongly-typed tenant configuration model in `FactoryOS.Configuration.Model`: `TenantConfiguration`
  (the `tenant.json` — the single artifact that onboards a factory), `ModuleConfiguration` and
  `PluginConfiguration` (sharing `IComponentConfiguration`: key + enabled + settings), `TenantBranding`,
  `TenantLocalization`, `DeploymentEnvironment` and `UnitSystem`, with `GetModule`/`IsModuleEnabled`/
  `GetPlugin`/`IsPluginEnabled` accessors.
- **Secrets**: `ISecretProvider` with `EnvironmentVariableSecretProvider` (prefixed env lookup) and
  `InMemorySecretProvider`, plus `SecretExpander` — resolves `${secret:NAME}` placeholders in setting
  values and fails loudly on any unresolved secret (source-generated regex).
- **Reading & validation**: `TenantConfigurationReader` (JSON → secret-expanded → validated
  `Result<TenantConfiguration>`), `ITenantConfigurationValidator` / `TenantConfigurationValidator`
  (fail-fast: id/name/duplicate module & plugin keys/branding/localization).
- **Reload**: `ITenantConfigurationSource` with `JsonTenantConfigurationSource`, and
  `ITenantConfigurationProvider` / `TenantConfigurationProvider` — eager fail-fast initial load, an
  atomically-swapped thread-safe snapshot, a `Changed` event, and reloads that only swap on success.
- DI: `AddFactoryConfiguration` (building blocks) and `AddTenantConfiguration(path)` (file-backed
  provider). Sample `sample.tenant.json`.
- 25 unit tests across secret expansion, env-var secrets, validation rules, reader (valid + secret
  expansion + missing secret + malformed + invalid environment + validation surface), accessors, and
  the reload provider (initial/failed-initial/swap+event/failed-reload).

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **112 passed** (108 unit + 4 architecture), 0 failed.

### Sprint 5 — Plugin Framework (2026-07-19)

Added
- Plugin **contracts** in `FactoryOS.Contracts.Plugins` (the stable SDK surface plugins depend on):
  `PluginVersion` (ordered `Major.Minor.Patch` value type with `Parse`/`TryParse`), `PluginDependency`
  (key + minimum version, `IsSatisfiedBy`), `PluginManifest` (the declarative `module.json`),
  `PluginState`, `IPlugin` (key + `ConfigureServices` + `StartAsync`/`StopAsync`) and the `PluginBase`
  convenience base class.
- Plugin **framework** in `FactoryOS.Plugin`:
  - `PluginManifestReader` — parses `module.json` into a validated manifest (`Result`-based).
  - `PluginDependencyResolver` — topological load-order resolution detecting duplicate keys, missing
    dependencies, unsatisfied version constraints and cycles.
  - `IPluginRegistry` / `PluginRegistry` — thread-safe descriptor catalogue with enable/disable.
  - `IPluginDiscovery` / `PluginDiscovery` — filesystem scan of `plugins/*/module.json`.
  - `PluginLoadContext` — collectible `AssemblyLoadContext` for isolated plugin loading (the seam to
    the Phase 5 out-of-process/sandboxed transition), deferring shared contracts to the default context.
  - `IPluginHost` / `PluginHost` — orchestrates the lifecycle: resolves order, configures services, and
    starts/stops plugins (reverse order on stop); disabled or instance-less descriptors are skipped/failed.
  - Source-generated host logging (`PluginLog`) and `AddPluginFramework` DI wiring.
- **Reference plugin** `plugins/sample` (`FactoryOS.Plugins.Sample`) depending only on `Contracts`:
  `SamplePlugin : PluginBase` registering `ISampleGreeter`, plus `module.json` and `sample.config.json`.
- 35 unit tests across version ordering, dependency satisfaction, manifest reading (valid + six failure
  modes), dependency resolution (order/duplicate/missing/version/cycle), registry, discovery (temp-dir
  scan), host lifecycle/order/skip, isolated assembly loading, and the sample plugin.

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **87 passed** (83 unit + 4 architecture), 0 failed.

### Sprint 4 — Event Bus (2026-07-19)

Added
- Event-bus **contracts** in `FactoryOS.Contracts.Events` (so modules depend on abstractions, never
  on the implementation): `IIntegrationEvent` / `IntegrationEvent` (time-ordered id + timestamp for
  idempotency), `IEventBus`, `IEventHandler<TEvent>`, `EventContext`, `EventPriority`,
  `EventPublishOptions`, `RetryPolicy` (exponential back-off), `DeadLetterEnvelope`,
  `IDeadLetterQueue`, `IEventBusMetrics`, and strongly-typed `EventBusOptions`.
- In-process **implementation** in `FactoryOS.EventBus`: `InProcessEventBus` (per-publish DI scope,
  handler resolution, per-handler retry, dead-lettering on exhaustion, correlation/trace propagation,
  handler-failure isolation), `InMemoryDeadLetterQueue`, `InMemoryEventBusMetrics`, and
  source-generated high-performance logging (`LoggerMessage`).
- 13 unit tests: dispatch, correlation/priority/attempt propagation, multi-handler fan-out,
  retry→dead-letter, transient recovery, no-handler and null-guard paths, retry-policy maths, and the
  in-memory dead-letter/metrics components.

Changed
- `.editorconfig`/`Directory.Build.props`: suppressed `CA1711` (idiomatic messaging names
  `IEventHandler`/`IDeadLetterQueue` deliberately keep their reserved suffixes).

Notes
- The default bus dispatches synchronously in-process; `EventPriority` is propagated end-to-end as
  message metadata (context, dead-letters, metrics) for priority-aware transports introduced later.

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **48 passed** (44 unit + 4 architecture), 0 failed.

### Sprint 3 — Domain Foundation (2026-07-19)

Added
- DDD tactical primitives in `FactoryOS.Domain`:
  - `BaseEntity` (domain-event machinery), `Entity<TId>` (identity equality), `AggregateRoot<TId>`
    (consistency boundary), `ValueObject` (structural equality), `IDomainEvent` / `DomainEvent`
    (version-7 GUID identity + UTC timestamp).
  - `Result` / `Result<TValue>` and `Error` / `ErrorType` for railway-oriented outcomes, with an
    implicit value-to-success conversion and guarded success/failure invariants.
  - `Specification<T>` with composable `And` / `Or` / `Not` combinators (expression-tree parameter
    rebinding so specifications translate to query predicates).
  - Abstractions `IRepository<TAggregate, TId>`, `IUnitOfWork`, `IDateTimeProvider`, `IIdGenerator`.
  - Default providers `SystemDateTimeProvider` and `SequentialGuidIdGenerator` (time-ordered GUIDs).
- `FactoryOS.Tests` unit suite (31 tests) covering entity/value-object equality, domain-event
  raising/clearing, `Result`/`Error` behaviour, specification composition and the providers.

Changed
- `.editorconfig`: `dotnet_style_require_accessibility_modifiers` set to `for_non_interface_members`
  (the idiomatic .NET default) so interface members stay clean.
- Test project suppresses `CA1859` (tests intentionally use interface-typed variables).

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **35 passed** (31 unit + 4 architecture), 0 failed.

### Sprint 2 — Clean Architecture (2026-07-19)

Added
- Clean Architecture project reference graph across all 12 `src` projects (dependencies point
  inward only): `Domain → Shared`; `Application → Domain, Contracts, Shared`;
  `Infrastructure` composes `Persistence, Identity, EventBus, Configuration, Core, Plugin`;
  `Api → Application, Infrastructure`.
- Strongly-typed DI composition — one `IServiceCollection` extension per layer
  (`AddApplication`, `AddEventBus`, `AddPluginFramework`, `AddCore`, `AddFactoryConfiguration`,
  `AddPersistence`, `AddIdentityModule`, `AddInfrastructure`), each with guard clauses.
- `FactoryOS.Api` composition root (`Program.cs`) wiring `AddApplication().AddInfrastructure(...)`
  plus a `/health` endpoint.
- `FactoryOS.ArchitectureTests` with `NetArchTest.Rules`, enforcing:
  Domain depends on no other layer; Application never depends on infrastructure-side layers;
  Contracts stays free of Domain and above; no layer depends on the Api host.
- Packages: `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.0 and
  `Microsoft.Extensions.Configuration.Abstractions` 10.0.0 on the composing layers;
  `NetArchTest.Rules` 1.3.2 on the architecture-test project.

Decisions
- **Composition root.** The Api host references only Application and the Infrastructure
  composition root; every other infrastructure concern is wired inside `AddInfrastructure`,
  keeping the host free of direct references to Persistence, Identity, EventBus, Plugin and Core.

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test` → **ArchitectureTests 4/4 passed**; solution exit 0.

### Sprint 1 — Solution skeleton (2026-07-19)

Added
- `FactoryOS.slnx` solution containing 15 projects (SDK 10 `.slnx` format).
  - `src/`: `FactoryOS.Domain`, `FactoryOS.Contracts`, `FactoryOS.Shared`,
    `FactoryOS.Application`, `FactoryOS.Core`, `FactoryOS.Plugin`, `FactoryOS.EventBus`,
    `FactoryOS.Configuration`, `FactoryOS.Persistence`, `FactoryOS.Identity`,
    `FactoryOS.Infrastructure`, `FactoryOS.Api`.
  - `tests/`: `FactoryOS.Tests`, `FactoryOS.ArchitectureTests`, `FactoryOS.IntegrationTests`.
- `Directory.Build.props` — central build policy enforcing the Constitution at compiler level:
  `net10.0`, nullable, implicit usings, `TreatWarningsAsErrors`, `GenerateDocumentationFile`
  (CS1591 as error → mandatory XML docs), latest-recommended analyzers.
- `tests/Directory.Build.props` — inherits root policy, relaxes XML-doc requirement for tests.
- `global.json` — pins SDK `10.0.302` (`rollForward: latestFeature`).
- `.editorconfig`, `.gitignore`, `.dockerignore`.
- `Dockerfile` — multi-stage, non-root, `sdk:10.0` → `aspnet:10.0`.
- `docker-compose.yml` — dev environment: PostgreSQL 17, RabbitMQ 4, Redis 7, MinIO, API.
- `AssemblyReference` marker type in each `src` library for assembly scanning / DI registration.

Decisions
- **Target framework: `net10.0` (LTS).** The stack originally named ASP.NET Core 9, but
  .NET 9 (STS) reached end of support ~May 2026; .NET 10 LTS is supported to ~Nov 2028 and
  matches the "long-lived" goal. Approved by the product owner.
- Suppressed analyzer `CA1716` globally (VB reserved-word namespace check) — the codebase is
  C#-only and project names are fixed by design (`FactoryOS.Shared`).

Verification
- `dotnet build FactoryOS.slnx -c Release` → **succeeded, 0 warnings, 0 errors**.
- `dotnet test FactoryOS.slnx -c Release` → **exit 0** (test suites present; test cases begin
  in Sprint 2/3 per the roadmap).

### Sprint 0 — Constitution (2026-07-19)

Added
- `docs/CONSTITUTION.md` — the 26 immutable rules governing the project.
- `CLAUDE.md` — operational architecture and build conventions bound to the Constitution.
- `README.md`, `docs/ROADMAP.md`, `docs/architecture/`, `docs/contracts/`.
