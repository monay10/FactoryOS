using FactoryOS.Connectors.Csv;
using FactoryOS.Connectors.Framework.Health;
using FactoryOS.Connectors.Framework.Runtime;
using FactoryOS.Connectors.Log;
using FactoryOS.Connectors.Log.Domain;
using FactoryOS.Connectors.Runtime.Discovery;
using FactoryOS.Connectors.Runtime.Domain;
using FactoryOS.Connectors.Runtime.Events;
using FactoryOS.Connectors.Runtime.Execution;
using FactoryOS.Connectors.Runtime.Integration;
using FactoryOS.Connectors.Runtime.Persistence;
using FactoryOS.Connectors.Runtime.Pipeline;
using FactoryOS.Connectors.Runtime.Security;
using FactoryOS.Contracts.Connectors;
using FactoryOS.Domain.Abstractions;
using FactoryOS.IntegrationTests.Persistence;
using FactoryOS.Plugins.Workflow.Audit.Domain;
using FactoryOS.Plugins.Workflow.Audit.Execution;
using FactoryOS.Plugins.Workflow.Monitoring.Domain;
using FactoryOS.Plugins.Workflow.Monitoring.Execution;
using FactoryOS.Plugins.Workflow.Security.Domain;
using FactoryOS.Plugins.Workflow.Security.Execution;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Connectors;

/// <summary>
/// Exercises the connector runtime through a real container, against the connectors this repository already
/// ships, and wired to the platform's real audit, monitoring and security engines through its ports.
/// </summary>
public sealed class ConnectorRuntimeIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 22, 09, 00, 00, TimeSpan.Zero);

    private const string Tenant = "acme";
    private const string Other = "borusan";

    // ---------------------------------------------------------------------------------------------------
    // Composition
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void The_container_composes_the_whole_runtime()
    {
        using var harness = new Harness();

        Assert.NotNull(harness.Engine);
        Assert.NotNull(harness.Provider.GetRequiredService<ConnectorRuntimeHost>());
        Assert.NotNull(harness.Provider.GetRequiredService<ConnectorScheduler>());
        Assert.Equal(12, harness.Provider.GetRequiredService<ConnectorPipeline>().Stages().Count);

        // The framework it builds on is still fully composed and untouched.
        Assert.NotNull(harness.Provider.GetRequiredService<IConnectorHealthService>());
        Assert.NotNull(harness.Provider.GetRequiredService<FactoryOS.Connectors.Framework.Registry.IConnectorRegistry>());
    }

    [Fact]
    public void The_runtime_registration_pulls_in_no_workflow_engine()
    {
        using var harness = new Harness();

        // The connector runtime is infrastructure. It must be usable in a host that has no workflow plugin at
        // all, so registering it may not drag an engine in behind the caller's back.
        Assert.Null(harness.Provider.GetService<AuditEngine>());
        Assert.Null(harness.Provider.GetService<MonitoringEngine>());
        Assert.Null(harness.Provider.GetService<SecurityEngine>());
    }

    // ---------------------------------------------------------------------------------------------------
    // Loading and discovery
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void The_connectors_this_repository_ships_are_discovered_from_their_manifests()
    {
        using var harness = new Harness();

        var summary = harness.Engine.Discover(Path.Combine(RepoRoot(), "connectors"));

        // Twelve inbound connectors declare the source system the manifest contract requires. The two
        // outbound ones — log and webhook — use the older outbound manifest shape, which carries a transport
        // rather than a source system, so discovery reports them by name with the reason instead of quietly
        // dropping them. They are loaded explicitly below, which is how an outbound connector reaches the
        // runtime today.
        Assert.Equal(12, summary.LoadedCount);
        Assert.Equal(2, summary.RejectedCount);
        Assert.All(summary.Rejected, rejected => Assert.NotNull(rejected.Error));
        Assert.Contains(summary.Loaded, definition => definition.Key == "logo");
        Assert.Contains(summary.Loaded, definition => definition.Key == "csv");
    }

    [Fact]
    public void A_discovered_definition_is_catalogued_but_nothing_can_be_asked_of_it_yet()
    {
        using var harness = new Harness();
        harness.Engine.Discover(Path.Combine(RepoRoot(), "connectors"));

        var instance = new ConnectorInstance(Tenant, "acme-logo", "logo", new ConnectorEndpoint("logo-db:1433"));
        Assert.True(harness.Engine.Activate(instance).IsSuccess);

        var start = harness.Engine.Start(Tenant, "acme-logo");

        Assert.True(start.IsFailure);
        Assert.Contains("NoHandler", start.Error.Code, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------------
    // Invoking connectors that already existed
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_csv_connector_this_repository_already_shipped_is_invocable_unchanged()
    {
        using var harness = new Harness();
        var csv = harness.WriteCsv("code,quantity\nITEM-1,10\nITEM-2,4\n");

        harness.LoadCsv(csv);

        var response = await harness.InvokeAsync("acme-csv", "read");

        Assert.True(response.Succeeded);
        var records = response.PayloadAs<List<SourceRecord>>();
        Assert.NotNull(records);
        Assert.Equal(2, records.Count);
        Assert.Equal("ITEM-1", records[0].Fields["code"]);
        Assert.Equal("2", response.Metadata["records"]);
        Assert.Equal("CSV", response.Metadata["sourceSystem"]);
    }

    [Fact]
    public async Task The_log_connector_delivers_out_through_the_same_pipeline()
    {
        using var harness = new Harness();
        var journal = new InMemoryDeliveryJournal();
        harness.LoadLog(journal);

        var response = await harness.Engine.InvokeAsync(
            ConnectorRequest.For(Tenant, "acme-log", "deliver") with
            {
                Caller = ConnectorCaller.Holding(Tenant, "u-ada", "connector.write"),
                Payload = Message(Tenant),
            });

        Assert.True(response.Succeeded);
        Assert.Equal("log", response.Metadata["transport"]);
        Assert.Single(journal.ForTenant(Tenant));
    }

    [Fact]
    public async Task A_message_belonging_to_another_tenant_is_never_delivered_through_this_ones_transport()
    {
        using var harness = new Harness();
        var journal = new InMemoryDeliveryJournal();
        harness.LoadLog(journal);

        var response = await harness.Engine.InvokeAsync(
            ConnectorRequest.For(Tenant, "acme-log", "deliver") with
            {
                Caller = ConnectorCaller.Holding(Tenant, "u-ada", "connector.write"),
                Payload = Message(Other),
            });

        Assert.False(response.Succeeded);
        Assert.Equal(ConnectorErrorKind.Forbidden, response.Error?.Kind);
        Assert.Empty(journal.ForTenant(Tenant));
    }

    [Fact]
    public async Task Each_factory_reads_its_own_source_and_cannot_reach_the_others()
    {
        using var harness = new Harness();

        // Each factory's source is its own connector definition. That is not a limitation of the runtime but
        // of the connectors it wraps: the ones this repository ships bind their source — a file path, a
        // connection string — at construction, so one configured connector is one definition. A connector
        // written against this runtime instead reads the endpoint from the invocation and serves every
        // instance; both shapes work, and neither was allowed to force a change on the other.
        harness.LoadCsv(harness.WriteCsv("code\nACME-1\n"), definitionKey: "csv-acme");
        harness.LoadCsv(
            harness.WriteCsv("code\nBORUSAN-1\n"),
            definitionKey: "csv-borusan",
            tenant: Other,
            instanceKey: "borusan-csv");

        var acme = await harness.InvokeAsync("acme-csv", "read");
        var borusan = await harness.InvokeAsync("borusan-csv", "read", tenant: Other);

        Assert.Equal("ACME-1", acme.PayloadAs<List<SourceRecord>>()![0].Fields["code"]);
        Assert.Equal("BORUSAN-1", borusan.PayloadAs<List<SourceRecord>>()![0].Fields["code"]);

        // There is no lookup by which either factory reaches the other's instance.
        var store = harness.Provider.GetRequiredService<IConnectorStore>();
        Assert.Null(store.Find(Other, "acme-csv"));
        Assert.Null(store.Find(Tenant, "borusan-csv"));
        Assert.Single(store.ListByTenant(Tenant));
    }

    [Fact]
    public void One_definition_is_shared_by_every_tenant_and_an_instance_belongs_to_exactly_one()
    {
        using var harness = new Harness();
        var path = harness.WriteCsv("code\nITEM-1\n");
        harness.LoadCsv(path);
        harness.LoadCsv(path, tenant: Other, instanceKey: "borusan-csv", load: false);

        Assert.Single(harness.Engine.Definitions(), definition => definition.Key == CsvConnector.ConnectorKey);

        var store = harness.Provider.GetRequiredService<IConnectorStore>();
        Assert.Equal(2, store.ListByDefinition(CsvConnector.ConnectorKey).Count);
        Assert.Equal(Tenant, Assert.Single(store.ListByTenant(Tenant)).Tenant);
        Assert.Equal(Other, Assert.Single(store.ListByTenant(Other)).Tenant);
    }

    // ---------------------------------------------------------------------------------------------------
    // Credentials and persistence
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_credential_is_stored_as_a_reference_and_resolved_only_when_it_is_used()
    {
        using var harness = new Harness();
        harness.Secrets.Set("ACME_CSV_KEY", "s3cret");

        var credential = new ConnectorCredential
        {
            Key = "acme-csv-key",
            Kind = ConnectorCredentialKind.ApiKey,
            SecretReference = "${secret:ACME_CSV_KEY}",
        };

        harness.Provider.GetRequiredService<IConnectorCredentialStore>().Save(Tenant, credential);
        harness.LoadCsv(harness.WriteCsv("code\nITEM-1\n"), credential: credential);

        var stored = harness.Provider.GetRequiredService<IConnectorCredentialStore>().Find(Tenant, "acme-csv-key");
        Assert.Equal("${secret:ACME_CSV_KEY}", stored?.SecretReference);
        Assert.DoesNotContain("s3cret", stored?.SecretReference, StringComparison.Ordinal);

        var response = await harness.InvokeAsync("acme-csv", "read");
        Assert.True(response.Succeeded);
    }

    [Fact]
    public void An_instance_whose_secret_is_gone_is_reported_by_the_host_and_the_others_still_start()
    {
        using var harness = new Harness();
        harness.LoadCsv(harness.WriteCsv("code\nITEM-1\n"));

        var broken = new ConnectorInstance(
            Tenant,
            "acme-csv-broken",
            CsvConnector.ConnectorKey,
            new ConnectorEndpoint("file:///missing.csv"),
            new ConnectorCredential
            {
                Key = "gone",
                Kind = ConnectorCredentialKind.ConnectionString,
                SecretReference = "${secret:ROTATED_AWAY}",
            });
        Assert.True(harness.Engine.Activate(broken).IsSuccess);

        var summary = harness.Engine.StartTenant(Tenant);

        Assert.False(summary.AllStarted);
        Assert.Contains("acme-csv", summary.Started);
        Assert.Contains("acme-csv-broken", summary.Failed.Keys);
        Assert.Contains("secret", summary.Failed["acme-csv-broken"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_instances_configuration_is_kept_where_it_can_be_diffed_and_reapplied()
    {
        using var harness = new Harness();
        harness.LoadCsv(harness.WriteCsv("code\nITEM-1\n"));

        var configurations = harness.Provider.GetRequiredService<IConnectorConfigurationRepository>();
        Assert.Empty(configurations.Get(Tenant, "acme-csv"));

        var result = harness.Engine.Instances.Reconfigure(
            Tenant,
            "acme-csv",
            new ConnectorEndpoint("file:///new.csv"),
            "u-ada",
            settings: new Dictionary<string, string?> { ["warehouse"] = "01" });

        Assert.True(result.IsSuccess);
        Assert.Equal("01", configurations.Get(Tenant, "acme-csv")["warehouse"]);
        Assert.Equal("u-ada", harness.Events.OfType<ConnectorConfigurationChanged>().Single().ChangedBy);
    }

    // ---------------------------------------------------------------------------------------------------
    // The platform's security engine, deciding through the port
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_platforms_security_engine_decides_whether_a_connector_may_be_invoked()
    {
        using var harness = new Harness(useSecurityEngine: true);
        harness.LoadCsv(harness.WriteCsv("code\nITEM-1\n"));

        var security = harness.Provider.GetRequiredService<SecurityEngine>();
        security.RegisterRole(new SecurityRole("integrator", "Integrator")
        {
            Permissions = ["connector.read"],
        });
        security.Grant(Tenant, "u-ada", "connector.read", "u-root");

        // The caller carries no permission of its own: everything it is allowed to do comes from the
        // security engine's own grants, so a pass here proves the engine really decided.
        var allowed = await harness.Engine.InvokeAsync(
            ConnectorRequest.For(Tenant, "acme-csv", "read") with
            {
                Caller = new ConnectorCaller(Tenant, "u-ada"),
            });

        var refused = await harness.Engine.InvokeAsync(
            ConnectorRequest.For(Tenant, "acme-csv", "read") with
            {
                Caller = new ConnectorCaller(Tenant, "u-mallory"),
            });

        Assert.True(allowed.Succeeded);
        Assert.False(refused.Succeeded);
        Assert.Equal(ConnectorErrorKind.Forbidden, refused.Error?.Kind);
    }

    [Fact]
    public async Task The_security_engine_refuses_a_caller_reaching_across_tenants()
    {
        using var harness = new Harness(useSecurityEngine: true);
        harness.LoadCsv(harness.WriteCsv("code\nITEM-1\n"));

        var security = harness.Provider.GetRequiredService<SecurityEngine>();
        security.Grant(Other, "u-eve", "connector.read", "u-root");

        var response = await harness.Engine.InvokeAsync(
            ConnectorRequest.For(Tenant, "acme-csv", "read") with
            {
                Caller = new ConnectorCaller(Other, "u-eve"),
            });

        Assert.False(response.Succeeded);
        Assert.Contains("TenantMismatch", response.Error?.Code, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------------
    // The platform's audit and monitoring engines, fed through the ports
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Every_invocation_reaches_the_platforms_audit_trail()
    {
        using var harness = new Harness(useAudit: true, useMonitoring: true);
        harness.LoadCsv(harness.WriteCsv("code\nITEM-1\n"));

        await harness.InvokeAsync("acme-csv", "read");
        await harness.InvokeAsync("acme-csv", "read", caller: ConnectorCaller.Holding(Tenant, "u-mallory", "workflow.read"));

        var audit = harness.Provider.GetRequiredService<AuditEngine>();
        var records = audit.ListByTenant(Tenant);

        Assert.Equal(2, records.Count);
        Assert.All(records, record => Assert.Equal(AuditCategory.Connector, record.Category));
        Assert.Contains(records, record => record.Result == AuditResult.Success);
        Assert.Contains(records, record => record.Result == AuditResult.Failure);

        // The trail the audit engine keeps is hash-chained; feeding it from a new source must not break that.
        Assert.True(audit.Verify(Tenant).IsValid);
    }

    [Fact]
    public async Task The_numbers_the_runtime_measures_reach_the_platforms_monitoring_engine()
    {
        using var harness = new Harness(useAudit: true, useMonitoring: true);
        harness.LoadCsv(harness.WriteCsv("code\nITEM-1\n"));

        await harness.InvokeAsync("acme-csv", "read");
        await harness.InvokeAsync("acme-csv", "read");

        var monitoring = harness.Provider.GetRequiredService<MonitoringEngine>();
        var instance = new MetricInstance(
            Tenant,
            ConnectorMetricNames.Invocations,
            MetricDimension.Of(
                MetricLabel.Of("connector", CsvConnector.ConnectorKey),
                MetricLabel.Of("operation", "read"),
                MetricLabel.Of("outcome", "success")));

        var snapshot = monitoring.Snapshot(instance, MetricAggregation.Sum);

        Assert.Equal(2, snapshot.Count);
        Assert.Equal(2, snapshot.Value);
        Assert.Contains(monitoring.Definitions(), definition => definition.Key == ConnectorMetricNames.Duration);
    }

    [Fact]
    public async Task A_refusal_is_measured_by_the_reason_it_was_refused_for()
    {
        using var harness = new Harness(useAudit: true, useMonitoring: true);
        harness.LoadCsv(harness.WriteCsv("code\nITEM-1\n"));

        await harness.InvokeAsync("acme-csv", "read", caller: ConnectorCaller.Holding(Tenant, "u-mallory", "workflow.read"));

        var monitoring = harness.Provider.GetRequiredService<MonitoringEngine>();
        var refusals = monitoring.Snapshot(
            new MetricInstance(
                Tenant,
                ConnectorMetricNames.Refusals,
                MetricDimension.Of(
                    MetricLabel.Of("connector", CsvConnector.ConnectorKey),
                    MetricLabel.Of("operation", "read"),
                    MetricLabel.Of("outcome", "forbidden"))),
            MetricAggregation.Sum);

        Assert.Equal(1, refusals.Value);
    }

    // ---------------------------------------------------------------------------------------------------
    // Health and scheduling
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Health_answers_every_aspect_and_the_worst_one_decides()
    {
        using var harness = new Harness();
        harness.LoadCsv(harness.WriteCsv("code\nITEM-1\n"));

        var unproven = harness.Engine.Health(Tenant, "acme-csv");
        Assert.Equal(5, unproven.Results.Count);
        Assert.Equal(ConnectorHealthStatus.Unknown, unproven.Status);

        await harness.InvokeAsync("acme-csv", "read");

        var proven = harness.Engine.Health(Tenant, "acme-csv");
        Assert.Equal(ConnectorHealthStatus.Healthy, proven.Status);
        Assert.Empty(proven.Problems);
    }

    [Fact]
    public async Task An_instance_that_is_stopped_reports_unhealthy_rather_than_silently_refusing()
    {
        using var harness = new Harness();
        harness.LoadCsv(harness.WriteCsv("code\nITEM-1\n"));
        await harness.InvokeAsync("acme-csv", "read");

        Assert.Equal(1, harness.Engine.StopTenant(Tenant, "maintenance"));

        var report = harness.Engine.Health(Tenant, "acme-csv");
        Assert.Equal(ConnectorHealthStatus.Unhealthy, report.Status);
        Assert.Equal(
            ConnectorHealthAspect.Liveness, report.For(ConnectorHealthAspect.Liveness)!.Aspect);
        Assert.Equal("maintenance", harness.Events.OfType<ConnectorStopped>().Single().Reason);
    }

    [Fact]
    public async Task A_schedule_polls_a_real_connector_when_it_is_due()
    {
        using var harness = new Harness();
        harness.LoadCsv(harness.WriteCsv("code\nITEM-1\n"));

        harness.Engine.Scheduler.Schedule(new ConnectorSchedule(
            "csv-poll",
            ConnectorRequest.For(Tenant, "acme-csv", "read") with
            {
                Caller = ConnectorCaller.Holding(Tenant, "svc-poller", "connector.read"),
            },
            TimeSpan.FromMinutes(5)));

        var first = await harness.Engine.RunDueAsync();
        var immediatelyAgain = await harness.Engine.RunDueAsync();

        harness.Clock.UtcNow = harness.Clock.UtcNow.AddMinutes(5);
        var afterTheInterval = await harness.Engine.RunDueAsync();

        Assert.True(first.Single().Response.Succeeded);
        Assert.Empty(immediatelyAgain);
        Assert.Single(afterTheInterval);
        Assert.Equal(2, harness.Engine.Scheduler.ListByTenant(Tenant).Single().Runs);
    }

    [Fact]
    public async Task Restarting_an_instance_closes_its_session_and_opens_a_new_one()
    {
        using var harness = new Harness();
        harness.LoadCsv(harness.WriteCsv("code\nITEM-1\n"));
        await harness.InvokeAsync("acme-csv", "read");

        var sessions = harness.Provider.GetRequiredService<ConnectorSessionManager>();
        var before = sessions.Find(Tenant, "acme-csv")!.Id;

        Assert.True(harness.Provider.GetRequiredService<ConnectorRuntimeHost>().Restart(Tenant, "acme-csv").IsSuccess);
        await harness.InvokeAsync("acme-csv", "read");

        Assert.NotEqual(before, sessions.Find(Tenant, "acme-csv")!.Id);
    }

    // ---------------------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------------------

    private static OutboundMessage Message(string tenant) => new()
    {
        Tenant = tenant,
        Channel = "ops",
        Priority = "Normal",
        Subject = "Line 3 stopped",
        Action = "Notify",
        OccurredAt = Now,
    };

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FactoryOS.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate the repository root.");
    }

    private static ConnectorManifest ManifestOf(string folder)
    {
        var path = Path.Combine(RepoRoot(), "connectors", folder, ConnectorRuntimeManifestReader.ManifestFileName);
        var result = FactoryOS.Connectors.Manifest.ConnectorManifestReader.ReadFile(path);
        return result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException($"Could not read '{folder}': {result.Error.Description}");
    }

    /// <summary>
    /// Presents the shipped <see cref="CsvConnector"/> under a deployment-specific key, so one connector class
    /// can serve several configured sources. The connector itself is untouched — only the key it is
    /// catalogued under differs, which is exactly what a second <c>connector.json</c> folder would do.
    /// </summary>
    private sealed class KeyedCsvConnector : IConnector
    {
        private readonly CsvConnector _inner;

        public KeyedCsvConnector(string key, CsvConnectorOptions options)
        {
            Key = key;
            _inner = new CsvConnector(options);
        }

        public string Key { get; }

        public string SourceSystem => _inner.SourceSystem;

        public IAsyncEnumerable<SourceRecord> ReadAsync(
            ConnectorReadContext context, CancellationToken cancellationToken) =>
            _inner.ReadAsync(context, cancellationToken);
    }

    /// <summary>Maps the connector runtime's authorization port onto the platform's security engine.</summary>
    private sealed class SecurityEngineAuthorizer : IConnectorAuthorizer
    {
        private readonly SecurityEngine _security;
        private readonly IDateTimeProvider _clock;

        public SecurityEngineAuthorizer(SecurityEngine security, IDateTimeProvider clock)
        {
            _security = security;
            _clock = clock;
        }

        public ConnectorAuthorization Authorize(
            ConnectorCaller? caller, ConnectorInstance instance, ConnectorOperation operation)
        {
            if (caller is null)
            {
                return ConnectorAuthorization.Deny(
                    ConnectorAuthorizationReason.NoCaller, "The request named nobody.");
            }

            var principal = new SecurityPrincipal(
                caller.Subject,
                caller.Tenant,
                new SecurityIdentity("connector-runtime", _clock.UtcNow),
                caller.Permissions.Select(permission =>
                    SecurityClaim.Of(SecurityClaim.PermissionType, permission)));

            var decision = _security.Authorize(principal, operation.Permission.ToString());
            if (decision.IsAllowed)
            {
                return ConnectorAuthorization.Allow(decision.Description);
            }

            var reason = decision.Reason switch
            {
                SecurityDecisionReason.TenantMismatch => ConnectorAuthorizationReason.TenantMismatch,
                SecurityDecisionReason.NotAuthenticated => ConnectorAuthorizationReason.NotAuthenticated,
                _ => ConnectorAuthorizationReason.MissingPermission,
            };

            return ConnectorAuthorization.Deny(reason, decision.Description);
        }
    }

    /// <summary>Maps the connector runtime's audit port onto the platform's audit engine.</summary>
    private sealed class AuditEngineSink : IConnectorAuditSink
    {
        private readonly AuditEngine _audit;

        public AuditEngineSink(AuditEngine audit) => _audit = audit;

        public void Record(ConnectorAuditEntry entry)
        {
            // The audit engine already speaks about connector calls: AuditCategory.Connector and a
            // ready-made entry. Nothing had to be added to it, and nothing was.
            _audit.Record(AuditEntries.ConnectorOperation(
                entry.Tenant,
                entry.Definition,
                entry.Operation,
                entry.Succeeded,
                new AuditCorrelation(
                    entry.Correlation.CorrelationId,
                    entry.Correlation.TraceId,
                    RequestId: entry.Correlation.RequestId)));
        }
    }

    /// <summary>Maps the connector runtime's metric port onto the platform's monitoring engine.</summary>
    private sealed class MonitoringEngineSink : IConnectorMetricSink
    {
        private readonly MonitoringEngine _monitoring;

        public MonitoringEngineSink(MonitoringEngine monitoring)
        {
            _monitoring = monitoring;

            foreach (var (key, kind, unit) in new[]
                     {
                         (ConnectorMetricNames.Invocations, MetricKind.Counter, "calls"),
                         (ConnectorMetricNames.Duration, MetricKind.Duration, "ms"),
                         (ConnectorMetricNames.Retries, MetricKind.Counter, "attempts"),
                         (ConnectorMetricNames.CacheHits, MetricKind.Counter, "hits"),
                         (ConnectorMetricNames.Throttled, MetricKind.Counter, "calls"),
                         (ConnectorMetricNames.CircuitRefusals, MetricKind.Counter, "calls"),
                         (ConnectorMetricNames.Refusals, MetricKind.Counter, "calls"),
                         (ConnectorMetricNames.Failures, MetricKind.Counter, "calls"),
                     })
            {
                _monitoring.Register(new MetricDefinition(
                    key, MetricCategory.Connector, kind, unit, $"Connector runtime: {key}."));
            }
        }

        public void Observe(ConnectorMeasurement measurement)
        {
            var dimension = new MetricDimension(
                measurement.Labels.Select(label => MetricLabel.Of(label.Key, label.Value)));

            _monitoring.Record(
                measurement.Tenant,
                measurement.Name,
                measurement.Value,
                dimension,
                timestampUtc: measurement.ObservedUtc);
        }
    }

    private sealed class Harness : IDisposable
    {
        private readonly List<string> _files = [];

        public Harness(bool useSecurityEngine = false, bool useAudit = false, bool useMonitoring = false)
        {
            Clock = new FixedClock(Now);
            Secrets = new InMemoryConnectorSecretSource();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            services.AddSingleton<IDateTimeProvider>(Clock);
            services.AddSingleton<IConnectorSecretSource>(Secrets);

            if (useSecurityEngine)
            {
                services.AddSecurityEngine();
                services.AddSingleton<IConnectorAuthorizer>(provider => new SecurityEngineAuthorizer(
                    provider.GetRequiredService<SecurityEngine>(),
                    provider.GetRequiredService<IDateTimeProvider>()));
            }

            if (useMonitoring)
            {
                services.AddMonitoringEngine();
                services.AddSingleton<IConnectorMetricSink>(provider =>
                    new MonitoringEngineSink(provider.GetRequiredService<MonitoringEngine>()));
            }

            if (useAudit)
            {
                services.AddAuditEngine();
                services.AddSingleton<IConnectorAuditSink>(provider =>
                    new AuditEngineSink(provider.GetRequiredService<AuditEngine>()));
            }

            services.AddConnectorRuntime();

            Provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
        }

        public FixedClock Clock { get; }

        public InMemoryConnectorSecretSource Secrets { get; }

        public ServiceProvider Provider { get; }

        public ConnectorEngine Engine => Provider.GetRequiredService<ConnectorEngine>();

        public InMemoryConnectorRuntimeEventSink Events =>
            Provider.GetRequiredService<InMemoryConnectorRuntimeEventSink>();

        public string WriteCsv(string content)
        {
            var path = Path.Combine(Path.GetTempPath(), $"factoryos-{Guid.NewGuid():N}.csv");
            File.WriteAllText(path, content);
            _files.Add(path);
            return path;
        }

        public void LoadCsv(
            string path,
            string tenant = Tenant,
            string instanceKey = "acme-csv",
            string? definitionKey = null,
            ConnectorCredential? credential = null,
            bool load = true)
        {
            var key = definitionKey ?? CsvConnector.ConnectorKey;
            var connector = new KeyedCsvConnector(key, new CsvConnectorOptions
            {
                FilePath = path,
                SourceEntity = "Inventory",
            });

            if (load)
            {
                var manifest = ManifestOf("csv") with { Key = key };
                Assert.True(Engine.Load(
                    ConnectorDefinition.FromManifest(
                        manifest,
                        new ConnectorVersion(1, 0, 0),
                        ConnectorCapability.Read,
                        ConnectorCategory.FileSystem),
                    new InboundConnectorOperationHandler(connector)).IsSuccess);
            }

            Assert.True(Engine.Activate(new ConnectorInstance(
                tenant,
                instanceKey,
                key,
                new ConnectorEndpoint($"file://{path}"),
                credential)).IsSuccess);
            Assert.True(Engine.Start(tenant, instanceKey).IsSuccess);
        }

        public void LoadLog(IDeliveryJournal journal)
        {
            var connector = new LogTransportConnector(journal, new LogConnectorOptions());
            var manifest = new ConnectorManifest
            {
                Key = LogTransportConnector.ConnectorKey,
                Name = "Log Transport Connector",
                SourceSystem = "log",
            };

            Assert.True(Engine.Load(
                ConnectorDefinition.FromManifest(
                    manifest, new ConnectorVersion(1, 0, 0), ConnectorCapability.Write),
                new OutboundConnectorOperationHandler(connector)).IsSuccess);

            Assert.True(Engine.Activate(new ConnectorInstance(
                Tenant, "acme-log", LogTransportConnector.ConnectorKey, new ConnectorEndpoint("journal"))).IsSuccess);
            Assert.True(Engine.Start(Tenant, "acme-log").IsSuccess);
        }

        public Task<ConnectorResponse> InvokeAsync(
            string instanceKey, string operation, string tenant = Tenant, ConnectorCaller? caller = null) =>
            Engine.InvokeAsync(ConnectorRequest.For(tenant, instanceKey, operation) with
            {
                Caller = caller ?? ConnectorCaller.Holding(tenant, "u-ada", "connector.read"),
            });

        public void Dispose()
        {
            Provider.Dispose();
            foreach (var file in _files.Where(File.Exists))
            {
                File.Delete(file);
            }
        }
    }
}
