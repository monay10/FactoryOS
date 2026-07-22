using System.Reflection;
using System.Text;
using FactoryOS.Connectors.Framework.Runtime;
using FactoryOS.Connectors.Runtime.Domain;
using FactoryOS.Connectors.Runtime.Execution;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Domain.Abstractions;
using FactoryOS.IntegrationTests.Persistence;
using FactoryOS.Plugin.Health;
using FactoryOS.Plugin.Hosting;
using FactoryOS.Plugins.Runtime.Configuration;
using FactoryOS.Plugins.Runtime.Discovery;
using FactoryOS.Plugins.Runtime.Domain;
using FactoryOS.Plugins.Runtime.Events;
using FactoryOS.Plugins.Runtime.Execution;
using FactoryOS.Plugins.Runtime.Integration;
using FactoryOS.Plugins.Runtime.Isolation;
using FactoryOS.Plugins.Runtime.Persistence;
using FactoryOS.Plugins.Runtime.Security;
using FactoryOS.Plugins.Workflow.Audit.Domain;
using FactoryOS.Plugins.Workflow.Audit.Execution;
using FactoryOS.Plugins.Workflow.Monitoring.Domain;
using FactoryOS.Plugins.Workflow.Monitoring.Execution;
using FactoryOS.Plugins.Workflow.Security.Domain;
using FactoryOS.Plugins.Workflow.Security.Execution;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FactoryOS.IntegrationTests.Plugins;

/// <summary>
/// Exercises the plugin runtime through a real container: a real plugin assembly loaded from disk, and the
/// platform's real security, audit and monitoring engines wired in through the runtime's ports.
/// </summary>
public sealed class PluginRuntimeIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 22, 09, 00, 00, TimeSpan.Zero);

    private const string Tenant = "acme";
    private const string Other = "borusan";
    private const string SampleKey = "sample";

    // ---------------------------------------------------------------------------------------------------
    // Composition
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void The_container_builds_and_hands_out_the_runtime_without_any_engine_present()
    {
        using var harness = new Harness();

        Assert.NotNull(harness.Provider.GetRequiredService<IPluginRuntime>());
        Assert.NotNull(harness.Provider.GetRequiredService<PluginEngine>());
        Assert.Null(harness.Provider.GetService<SecurityEngine>());
        Assert.Null(harness.Provider.GetService<AuditEngine>());
        Assert.Null(harness.Provider.GetService<MonitoringEngine>());
    }

    [Fact]
    public void Registering_the_runtime_registers_the_framework_it_builds_on()
    {
        using var harness = new Harness();

        Assert.NotNull(harness.Provider.GetRequiredService<FactoryOS.Plugin.Registry.IPluginRegistry>());
        Assert.NotNull(harness.Provider.GetRequiredService<IPluginHealthService>());
        Assert.NotNull(harness.Provider.GetRequiredService<FactoryOS.Plugin.Catalog.IPluginCatalog>());
    }

    // ---------------------------------------------------------------------------------------------------
    // Loading a real plugin from disk
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_real_plugin_assembly_is_discovered_installed_loaded_and_started_from_disk()
    {
        using var harness = new Harness();
        var root = harness.StageSamplePlugin();

        var discovered = harness.Runtime.Discover(root);
        var package = Assert.Single(discovered.Packages);
        Assert.Equal(SampleKey, package.Key);

        await harness.Install(package);
        Assert.True((await harness.Runtime.Lifecycle.LoadAsync(Harness.Admin(Tenant), SampleKey)).IsSuccess);
        Assert.True((await harness.Runtime.Lifecycle.StartAsync(Harness.Admin(Tenant), SampleKey)).IsSuccess);

        var instance = harness.Registry.Find(Tenant, SampleKey)!;
        Assert.Equal(PluginRuntimeStatus.Running, instance.Status);
        Assert.Equal(SampleKey, harness.Registry.Attached(instance)!.Key);
    }

    [Fact]
    public async Task A_plugin_loaded_from_disk_gets_its_own_load_context_which_unload_releases()
    {
        using var harness = new Harness();
        var root = harness.StageSamplePlugin();

        var package = Assert.Single(harness.Runtime.Discover(root).Packages);
        await harness.Install(package);
        await harness.Runtime.Lifecycle.LoadAsync(Harness.Admin(Tenant), SampleKey);

        var instance = harness.Registry.Find(Tenant, SampleKey)!;
        Assert.True(harness.Isolation.IsIsolated(instance));
        Assert.Equal(1, harness.Isolation.LoadedContexts);

        await harness.Runtime.Lifecycle.UnloadAsync(Harness.Admin(Tenant), SampleKey);

        // The framework can load a plugin but never holds its context, so it can never unload one. The
        // runtime keeps the reference, which is exactly what makes update, rollback and remove possible.
        Assert.False(harness.Isolation.IsIsolated(instance));
        Assert.Equal(0, harness.Isolation.LoadedContexts);
        Assert.Null(harness.Registry.Attached(instance));
    }

    [Fact]
    public async Task The_manifests_own_ui_screens_arrive_as_extension_contributions()
    {
        using var harness = new Harness();
        var root = harness.StageSamplePlugin();

        var package = Assert.Single(harness.Runtime.Discover(root).Packages);
        await harness.Install(package, Harness.Grants(PluginPermission.Parse("uimetadata.extend")));
        await harness.Start(SampleKey);

        var screens = harness.Runtime.Extensions(Tenant, PluginExtensionPointKind.UiMetadata);

        Assert.Equal(2, screens.Count);
        Assert.Contains(screens, screen => screen.Contribution.Name == "sample.home");
    }

    [Fact]
    public async Task Two_factories_load_the_same_assembly_into_two_contexts()
    {
        using var harness = new Harness();
        var root = harness.StageSamplePlugin();
        var package = Assert.Single(harness.Runtime.Discover(root).Packages);

        await harness.Install(package);
        await harness.Install(package, tenant: Other);
        await harness.Runtime.Lifecycle.LoadAsync(Harness.Admin(Tenant), SampleKey);
        await harness.Runtime.Lifecycle.LoadAsync(Harness.Admin(Other), SampleKey);

        Assert.Equal(2, harness.Isolation.LoadedContexts);
        Assert.NotSame(
            harness.Registry.Attached(harness.Registry.Find(Tenant, SampleKey)!),
            harness.Registry.Attached(harness.Registry.Find(Other, SampleKey)!));
    }

    // ---------------------------------------------------------------------------------------------------
    // Signature validation of a real staged package
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_package_signed_with_a_trusted_key_installs_and_a_tampered_manifest_does_not()
    {
        using var harness = new Harness(requireSignature: true);
        var key = Encoding.UTF8.GetBytes("integration-signing-key");
        harness.Keys.Add("store", key);

        var root = harness.StageSamplePlugin();
        var package = Assert.Single(harness.Runtime.Discover(root).Packages);
        var signed = package with
        {
            Signature = PluginSignature.Hmac(PluginSignatureValidator.Sign(package, key), "store"),
        };

        Assert.True((await harness.Runtime.InstallAsync(
            Harness.Admin(Tenant), signed, Harness.Grants())).IsSuccess);

        var tampered = signed with
        {
            Definition = signed.Definition with { EntryType = "Somewhere.Else.Plugin" },
        };

        var refused = await harness.Runtime.InstallAsync(Harness.Admin(Other), tampered, Harness.Grants());
        Assert.True(refused.IsFailure);
        Assert.Equal("Plugin.Runtime.Signature.Invalid", refused.Error.Code);
    }

    [Fact]
    public async Task A_host_that_requires_signing_refuses_the_repositorys_own_unsigned_packages()
    {
        using var harness = new Harness(requireSignature: true);
        var root = harness.StageSamplePlugin();

        var package = Assert.Single(harness.Runtime.Discover(root).Packages);
        var refused = await harness.Runtime.InstallAsync(Harness.Admin(Tenant), package, Harness.Grants());

        Assert.True(refused.IsFailure);
        Assert.Equal("Plugin.Runtime.Signature.Missing", refused.Error.Code);
    }

    // ---------------------------------------------------------------------------------------------------
    // Update and rollback of a real package
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_real_plugin_is_updated_and_rolled_back_while_the_tenant_keeps_running()
    {
        using var harness = new Harness();
        var root = harness.StageSamplePlugin();
        var package = Assert.Single(harness.Runtime.Discover(root).Packages);

        await harness.Install(package);
        await harness.Start(SampleKey);

        var next = package with { Definition = package.Definition with { Version = new PluginVersion(1, 1, 0) } };
        Assert.True((await harness.Runtime.Updates.UpdateAsync(Harness.Admin(Tenant), next)).IsSuccess);
        Assert.Equal(new PluginVersion(1, 1, 0), harness.Registry.Find(Tenant, SampleKey)!.Version);
        Assert.Equal(PluginRuntimeStatus.Running, harness.Registry.Find(Tenant, SampleKey)!.Status);

        Assert.True((await harness.Runtime.Updates.RollbackAsync(Harness.Admin(Tenant), SampleKey)).IsSuccess);
        Assert.Equal(new PluginVersion(1, 0, 0), harness.Registry.Find(Tenant, SampleKey)!.Version);
        Assert.Equal(PluginRuntimeStatus.Running, harness.Registry.Find(Tenant, SampleKey)!.Status);
    }

    [Fact]
    public async Task An_update_stores_both_versions_so_the_rollback_has_a_package_to_return_to()
    {
        using var harness = new Harness();
        var root = harness.StageSamplePlugin();
        var package = Assert.Single(harness.Runtime.Discover(root).Packages);

        await harness.Install(package);
        await harness.Runtime.Updates.UpdateAsync(
            Harness.Admin(Tenant),
            package with { Definition = package.Definition with { Version = new PluginVersion(1, 1, 0) } });

        var packages = harness.Provider.GetRequiredService<IPluginPackageStore>();

        Assert.Equal(2, packages.Versions(SampleKey).Count);
        Assert.NotNull(packages.Find(SampleKey, new PluginVersion(1, 0, 0)));
    }

    // ---------------------------------------------------------------------------------------------------
    // Security engine integration
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_platforms_security_engine_decides_who_may_drive_a_plugin()
    {
        using var harness = new Harness(useSecurityEngine: true);
        var root = harness.StageSamplePlugin();
        var package = Assert.Single(harness.Runtime.Discover(root).Packages);

        var security = harness.Provider.GetRequiredService<SecurityEngine>();
        security.Grant(Tenant, "ops", "plugin.*", grantedBy: "integration-test");

        await harness.Install(package);
        Assert.True((await harness.Runtime.Lifecycle.LoadAsync(Harness.Admin(Tenant), SampleKey)).IsSuccess);

        var viewer = PluginCaller.Holding(Tenant, "viewer");
        var refused = await harness.Runtime.Lifecycle.StartAsync(viewer, SampleKey);

        Assert.True(refused.IsFailure);
        Assert.Equal("Plugin.Runtime.Forbidden", refused.Error.Code);
    }

    [Fact]
    public async Task The_security_engine_cannot_be_talked_into_letting_a_caller_cross_tenants()
    {
        using var harness = new Harness(useSecurityEngine: true);
        var root = harness.StageSamplePlugin();
        var package = Assert.Single(harness.Runtime.Discover(root).Packages);

        var security = harness.Provider.GetRequiredService<SecurityEngine>();
        security.Grant(Tenant, "ops", "plugin.*", grantedBy: "integration-test");
        security.Grant(Other, "ops", "plugin.*", grantedBy: "integration-test");

        await harness.Install(package);

        // The instance belongs to 'acme'. The caller is a fully authorized administrator — of 'borusan'.
        var trespasser = PluginCaller.Holding(Other, "ops", PluginPermission.Parse("plugin.*"));
        var refused = await harness.Runtime.Lifecycle.StartAsync(trespasser, SampleKey);

        Assert.True(refused.IsFailure);
        Assert.Equal("Plugin.Runtime.NotInstalled", refused.Error.Code);
    }

    [Fact]
    public void The_gate_refuses_a_cross_tenant_request_even_with_the_security_engine_behind_it()
    {
        using var harness = new Harness(useSecurityEngine: true);
        var security = harness.Provider.GetRequiredService<SecurityEngine>();
        security.Grant(Other, "ops", SecurityPermissions.All, grantedBy: "integration-test");

        var gate = harness.Provider.GetRequiredService<PluginAuthorizationGate>();
        var instance = new PluginInstance(Tenant, SampleKey, new PluginVersion(1, 0, 0));
        var trespasser = PluginCaller.Holding(Other, "ops", PluginPermission.Parse("*.*"));

        var refused = gate.Check(trespasser, instance, PluginLifecyclePhase.Start);

        Assert.True(refused.IsFailure);
        Assert.Contains("belongs to", refused.Error.Description, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------------
    // Audit engine integration
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Plugin_lifecycle_lands_in_the_platforms_audit_trail()
    {
        using var harness = new Harness(useAudit: true);
        var root = harness.StageSamplePlugin();
        var package = Assert.Single(harness.Runtime.Discover(root).Packages);

        await harness.Install(package);
        await harness.Start(SampleKey);

        var audit = harness.Provider.GetRequiredService<AuditEngine>();
        var records = audit.ListByTenant(Tenant);

        Assert.NotEmpty(records);
        Assert.All(records, record => Assert.Equal(AuditCategory.Plugin, record.Category));
        Assert.Contains(records, record => record.Message.Contains("Start", StringComparison.Ordinal));
        Assert.True(audit.Verify(Tenant).IsValid);
    }

    [Fact]
    public async Task A_refused_transition_is_audited_with_the_reason_it_was_refused()
    {
        using var harness = new Harness(useAudit: true);
        var root = harness.StageSamplePlugin();
        var package = Assert.Single(harness.Runtime.Discover(root).Packages);

        await harness.Install(package);

        var viewer = PluginCaller.Holding(Tenant, "viewer", PluginPermissions.Observe);
        await harness.Runtime.Lifecycle.RemoveAsync(viewer, SampleKey);

        var audit = harness.Provider.GetRequiredService<AuditEngine>();

        Assert.Contains(
            audit.ListByTenant(Tenant),
            record => record.Message.Contains("refused", StringComparison.OrdinalIgnoreCase));
    }

    // ---------------------------------------------------------------------------------------------------
    // Monitoring engine integration
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Plugin_lifecycle_produces_metrics_the_platforms_monitoring_engine_accepts()
    {
        using var harness = new Harness(useMonitoring: true);
        var root = harness.StageSamplePlugin();
        var package = Assert.Single(harness.Runtime.Discover(root).Packages);

        await harness.Install(package);
        await harness.Start(SampleKey);

        var monitoring = harness.Provider.GetRequiredService<MonitoringEngine>();
        var snapshot = monitoring.Snapshot(
            new MetricInstance(
                Tenant,
                PluginMetricNames.Transitions,
                MetricDimension.Of(
                    MetricLabel.Of(PluginRuntimeConstants.PluginLabel, SampleKey),
                    MetricLabel.Of(PluginRuntimeConstants.PhaseLabel, nameof(PluginLifecyclePhase.Start)),
                    MetricLabel.Of(PluginRuntimeConstants.OutcomeLabel, "success"))),
            MetricAggregation.Sum);

        Assert.Equal(1, snapshot.Value);
        Assert.Contains(
            monitoring.Definitions(),
            definition => definition.Key == PluginMetricNames.Transitions
                && definition.Category == MetricCategory.Plugin);
    }

    [Fact]
    public async Task One_factorys_measurements_never_appear_under_another()
    {
        using var harness = new Harness(useMonitoring: true);
        var root = harness.StageSamplePlugin();
        var package = Assert.Single(harness.Runtime.Discover(root).Packages);

        await harness.Install(package);

        var monitoring = harness.Provider.GetRequiredService<MonitoringEngine>();
        var dimension = MetricDimension.Of(
            MetricLabel.Of(PluginRuntimeConstants.PluginLabel, SampleKey),
            MetricLabel.Of(PluginRuntimeConstants.PhaseLabel, nameof(PluginLifecyclePhase.Install)),
            MetricLabel.Of(PluginRuntimeConstants.OutcomeLabel, "success"));

        Assert.Equal(
            1,
            monitoring.Snapshot(
                new MetricInstance(Tenant, PluginMetricNames.Transitions, dimension),
                MetricAggregation.Sum).Value);
        Assert.True(
            monitoring.Snapshot(
                new MetricInstance(Other, PluginMetricNames.Transitions, dimension),
                MetricAggregation.Sum).IsEmpty);
    }

    // ---------------------------------------------------------------------------------------------------
    // Connector runtime integration
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_plugin_contributes_a_connector_and_the_connector_runtime_takes_it_from_data_alone()
    {
        using var harness = new Harness(useConnectorRuntime: true, compiled: new StubPlugin(SampleKey));

        var definition = PluginDefinition.FromManifest(SampleManifest()) with
        {
            Contributions =
            [
                PluginContribution.To(PluginExtensionPointKind.Connector, "shopfloor-csv") with
                {
                    Reference = "csv",
                },
            ],
        };

        await harness.Install(
            PluginPackage.WithoutSignature(SampleManifest(), definition),
            Harness.Grants(PluginPermission.Parse("connector.extend")));
        await harness.Start(SampleKey);

        var contributed = Assert.Single(harness.Runtime.Extensions(Tenant, PluginExtensionPointKind.Connector));

        // The two runtimes meet here and nowhere else: the plugin runtime hands over a name and a reference,
        // and the connector runtime registers a definition. Neither project references the other.
        var connectors = harness.Provider.GetRequiredService<ConnectorEngine>();
        var registered = connectors.Register(new ConnectorDefinition
        {
            Key = contributed.Contribution.Name,
            Name = contributed.Contribution.Name,
            Version = new ConnectorVersion(1, 0, 0),
            SourceSystem = contributed.Contribution.Reference!,
            Capabilities = ConnectorCapability.Read,
            Operations = [ConnectorOperation.Read()],
        });

        Assert.True(registered.IsSuccess);
        Assert.Contains(connectors.Definitions(), definition => definition.Key == "shopfloor-csv");
    }

    [Fact]
    public async Task A_plugin_that_stops_withdraws_the_connector_it_contributed()
    {
        using var harness = new Harness(useConnectorRuntime: true, compiled: new StubPlugin(SampleKey));

        var definition = PluginDefinition.FromManifest(SampleManifest()) with
        {
            Contributions = [PluginContribution.To(PluginExtensionPointKind.Connector, "shopfloor-csv")],
        };

        await harness.Install(
            PluginPackage.WithoutSignature(SampleManifest(), definition),
            Harness.Grants(PluginPermission.Parse("connector.extend")));
        await harness.Start(SampleKey);
        Assert.Single(harness.Runtime.Extensions(Tenant, PluginExtensionPointKind.Connector));

        await harness.Runtime.Lifecycle.StopAsync(Harness.Admin(Tenant), SampleKey);

        Assert.Empty(harness.Runtime.Extensions(Tenant, PluginExtensionPointKind.Connector));
    }

    // ---------------------------------------------------------------------------------------------------
    // Persistence, health and the cold start
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task What_a_tenant_installed_survives_in_the_store_and_the_catalogue()
    {
        using var harness = new Harness();
        var root = harness.StageSamplePlugin();
        var package = Assert.Single(harness.Runtime.Discover(root).Packages);

        await harness.Install(package);

        var store = harness.Provider.GetRequiredService<IPluginStore>();
        var repository = harness.Provider.GetRequiredService<IPluginRepository>();
        var manifests = harness.Provider.GetRequiredService<IPluginManifestRepository>();

        Assert.NotNull(store.Find(Tenant, SampleKey));
        Assert.NotNull(repository.Find(SampleKey, new PluginVersion(1, 0, 0)));
        Assert.NotNull(manifests.Find(SampleKey, new PluginVersion(1, 0, 0)));
        Assert.Empty(store.ForTenant(Other));
    }

    [Fact]
    public async Task A_cold_start_brings_a_factory_up_from_a_folder_of_packages()
    {
        using var harness = new Harness();
        var root = harness.StageSamplePlugin();

        var result = await harness.Engine.BootstrapAsync(Harness.Admin(Tenant), Harness.Grants(), root);

        Assert.True(result.IsClean, string.Join(" | ", result.Problems));
        Assert.Equal(1, result.Started);
        Assert.Equal(PluginHealthStatus.Healthy, harness.Runtime.Health(Tenant, SampleKey).Status);
    }

    [Fact]
    public async Task A_scheduled_sweep_reports_on_everything_installed()
    {
        using var harness = new Harness();
        var root = harness.StageSamplePlugin();
        await harness.Engine.BootstrapAsync(Harness.Admin(Tenant), Harness.Grants(), root);

        var reports = harness.Provider.GetRequiredService<PluginRuntimeScheduler>().RunDue(Now);

        var report = Assert.Single(reports);
        Assert.Equal(SampleKey, report.PluginKey);
        Assert.Equal(PluginHealthStatus.Healthy, report.Status);
    }

    [Fact]
    public async Task Removing_a_plugin_from_one_tenant_leaves_the_package_for_every_other()
    {
        using var harness = new Harness();
        var root = harness.StageSamplePlugin();
        var package = Assert.Single(harness.Runtime.Discover(root).Packages);

        await harness.Install(package);
        await harness.Install(package, tenant: Other);
        await harness.Runtime.Lifecycle.RemoveAsync(Harness.Admin(Tenant), SampleKey);

        Assert.Empty(harness.Runtime.Installed(Tenant));
        Assert.Single(harness.Runtime.Installed(Other));
        Assert.NotNull(harness.Provider.GetRequiredService<IPluginPackageStore>()
            .Find(SampleKey, new PluginVersion(1, 0, 0)));
        Assert.Single(harness.Events.Of<PluginRemoved>());
    }

    // ---------------------------------------------------------------------------------------------------
    // Documentation
    // ---------------------------------------------------------------------------------------------------

    [Fact]
    public void The_runtime_ships_a_readme_that_explains_what_it_adds_and_a_sample_configuration()
    {
        var runtime = Path.Combine(RepositoryRoot(), "src", "FactoryOS.Plugin", "PluginRuntime");

        var readme = File.ReadAllText(Path.Combine(runtime, "README.md"));
        Assert.Contains("FactoryOS.Plugins.Runtime", readme, StringComparison.Ordinal);
        Assert.Contains("signature", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rollback", readme, StringComparison.OrdinalIgnoreCase);

        var sample = File.ReadAllText(Path.Combine(runtime, "sample.config.json"));
        Assert.Contains("Plugins", sample, StringComparison.Ordinal);
        Assert.DoesNotContain("BEGIN PRIVATE KEY", sample, StringComparison.Ordinal);
    }

    [Fact]
    public void The_changelog_records_this_commit()
    {
        var changelog = File.ReadAllText(Path.Combine(RepositoryRoot(), "CHANGELOG.md"));

        Assert.Contains("Commit 0022", changelog, StringComparison.Ordinal);
        Assert.Contains("Plugin runtime", changelog, StringComparison.OrdinalIgnoreCase);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FactoryOS.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }

    private static PluginManifest SampleManifest() => new()
    {
        Key = SampleKey,
        Name = "Sample Plugin",
        Version = new PluginVersion(1, 0, 0),
    };

    /// <summary>A plugin compiled into the host, for the cases that are not about loading from disk.</summary>
    private sealed class StubPlugin : IPlugin
    {
        public StubPlugin(string key) => Key = key;

        public string Key { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            // The runtime never calls this; the host's composition root does.
        }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>Maps the plugin runtime's authorization port onto the platform's security engine.</summary>
    private sealed class SecurityEnginePluginAuthorizer : IPluginAuthorizer
    {
        private readonly SecurityEngine _security;
        private readonly IDateTimeProvider _clock;

        public SecurityEnginePluginAuthorizer(SecurityEngine security, IDateTimeProvider clock)
        {
            _security = security;
            _clock = clock;
        }

        public PluginAuthorization Authorize(
            PluginCaller? caller, PluginInstance instance, PluginPermission required)
        {
            if (caller is null)
            {
                return PluginAuthorization.Deny(
                    PluginAuthorizationReason.NoCaller, "The request named nobody.");
            }

            var principal = new SecurityPrincipal(
                caller.Subject,
                caller.Tenant,
                new SecurityIdentity("plugin-runtime", _clock.UtcNow));

            var decision = _security.Authorize(principal, required.ToString());
            if (decision.IsAllowed)
            {
                return PluginAuthorization.Allow();
            }

            var reason = decision.Reason switch
            {
                SecurityDecisionReason.TenantMismatch => PluginAuthorizationReason.TenantMismatch,
                SecurityDecisionReason.NotAuthenticated => PluginAuthorizationReason.NotAuthenticated,
                _ => PluginAuthorizationReason.MissingPermission,
            };

            return PluginAuthorization.Deny(reason, decision.Description);
        }
    }

    /// <summary>Maps the plugin runtime's audit port onto the platform's audit engine.</summary>
    private sealed class AuditEnginePluginSink : IPluginAuditSink
    {
        private readonly AuditEngine _audit;

        public AuditEnginePluginSink(AuditEngine audit) => _audit = audit;

        public void Write(PluginAuditEntry entry)
        {
            // The audit engine already speaks about plugin lifecycle: AuditCategory.Plugin and a ready-made
            // entry. Nothing had to be added to it, and nothing was.
            var outcome = entry.Succeeded ? entry.Phase.ToString() : $"{entry.Phase} refused: {entry.FailureReason}";

            _audit.Record(AuditEntries.PluginOperation(
                entry.Tenant,
                entry.PluginKey,
                outcome,
                entry.Subject is null ? null : AuditActor.User(entry.Subject)));
        }
    }

    /// <summary>Maps the plugin runtime's metric port onto the platform's monitoring engine.</summary>
    private sealed class MonitoringEnginePluginSink : IPluginMetricSink
    {
        private readonly MonitoringEngine _monitoring;

        public MonitoringEnginePluginSink(MonitoringEngine monitoring)
        {
            _monitoring = monitoring;

            foreach (var (key, kind, unit) in new[]
                     {
                         (PluginMetricNames.Transitions, MetricKind.Counter, "transitions"),
                         (PluginMetricNames.TransitionDuration, MetricKind.Duration, "ms"),
                         (PluginMetricNames.Failures, MetricKind.Counter, "transitions"),
                         (PluginMetricNames.Installs, MetricKind.Counter, "plugins"),
                         (PluginMetricNames.Starts, MetricKind.Counter, "plugins"),
                         (PluginMetricNames.Stops, MetricKind.Counter, "plugins"),
                         (PluginMetricNames.Updates, MetricKind.Counter, "plugins"),
                         (PluginMetricNames.Rollbacks, MetricKind.Counter, "plugins"),
                         (PluginMetricNames.SandboxRefusals, MetricKind.Counter, "calls"),
                     })
            {
                _monitoring.Register(new MetricDefinition(
                    key, MetricCategory.Plugin, kind, unit, $"Plugin runtime: {key}."));
            }
        }

        public void Record(PluginMeasurement measurement)
        {
            var tenant = measurement.Labels[PluginRuntimeConstants.TenantLabel];
            var dimension = new MetricDimension(
                measurement.Labels
                    .Where(label => label.Key != PluginRuntimeConstants.TenantLabel)
                    .Select(label => MetricLabel.Of(label.Key, label.Value)));

            _monitoring.Record(
                tenant, measurement.Name, measurement.Value, dimension, timestampUtc: measurement.OccurredUtc);
        }
    }

    private sealed class Harness : IDisposable
    {
        private readonly List<string> _directories = [];

        public Harness(
            bool useSecurityEngine = false,
            bool useAudit = false,
            bool useMonitoring = false,
            bool useConnectorRuntime = false,
            bool requireSignature = false,
            IPlugin? compiled = null)
        {
            Clock = new FixedClock(Now);
            Keys = new InMemoryPluginSigningKeySource();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            services.AddSingleton<ILogger<PluginHost>>(NullLogger<PluginHost>.Instance);
            services.AddSingleton<IDateTimeProvider>(Clock);
            services.AddSingleton<IPluginSigningKeySource>(Keys);

            if (compiled is not null)
            {
                services.AddSingleton(compiled);
            }

            if (useSecurityEngine)
            {
                services.AddSecurityEngine();
                services.AddSingleton<IPluginAuthorizer>(provider => new SecurityEnginePluginAuthorizer(
                    provider.GetRequiredService<SecurityEngine>(),
                    provider.GetRequiredService<IDateTimeProvider>()));
            }

            if (useAudit)
            {
                services.AddAuditEngine();
                services.AddSingleton<IPluginAuditSink>(provider =>
                    new AuditEnginePluginSink(provider.GetRequiredService<AuditEngine>()));
            }

            if (useMonitoring)
            {
                services.AddMonitoringEngine();
                services.AddSingleton<IPluginMetricSink>(provider =>
                    new MonitoringEnginePluginSink(provider.GetRequiredService<MonitoringEngine>()));
            }

            if (useConnectorRuntime)
            {
                services.AddConnectorRuntime();
            }

            services.AddPluginRuntime();
            services.Configure<PluginRuntimeOptions>(options => options.RequireSignature = requireSignature);

            Provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
        }

        public FixedClock Clock { get; }

        public InMemoryPluginSigningKeySource Keys { get; }

        public ServiceProvider Provider { get; }

        public IPluginRuntime Runtime => Provider.GetRequiredService<IPluginRuntime>();

        public PluginEngine Engine => Provider.GetRequiredService<PluginEngine>();

        public PluginInstanceRegistry Registry => Provider.GetRequiredService<PluginInstanceRegistry>();

        public PluginIsolationManager Isolation => Provider.GetRequiredService<PluginIsolationManager>();

        public InMemoryPluginRuntimeEventSink Events =>
            Provider.GetRequiredService<InMemoryPluginRuntimeEventSink>();

        public static PluginCaller Admin(string tenant) =>
            PluginCaller.Holding(tenant, "ops", PluginPermission.Parse("plugin.*"));

        /// <summary>
        /// The grant a tenant gives the sample plugin. It includes <c>uimetadata.extend</c> because the
        /// sample's manifest declares UI screens, and a screen is a contribution to a published extension
        /// point like any other — the runtime refuses to install a plugin whose contributions are ungranted.
        /// </summary>
        public static IReadOnlyList<PluginPermission> Grants(params PluginPermission[] extra) =>
            [PluginPermission.Parse("plugin.*"), PluginPermission.Parse("uimetadata.extend"), .. extra];

        /// <summary>
        /// Stages the repository's own sample plugin as a package folder on disk: its manifest beside the
        /// assembly the test run already built. This is a real plugin, really loaded.
        /// </summary>
        public string StageSamplePlugin()
        {
            var root = Path.Combine(Path.GetTempPath(), "factoryos-pluginruntime-" + Guid.NewGuid().ToString("N"));
            var folder = Path.Combine(root, SampleKey);
            Directory.CreateDirectory(folder);
            _directories.Add(root);

            var assemblyName = typeof(FactoryOS.Plugins.Sample.SamplePlugin).Assembly.GetName().Name + ".dll";
            var source = Path.Combine(AppContext.BaseDirectory, assemblyName);
            File.Copy(source, Path.Combine(folder, assemblyName), overwrite: true);

            File.Copy(
                Path.Combine(RepositoryRoot(), "plugins", SampleKey, "module.json"),
                Path.Combine(folder, "module.json"),
                overwrite: true);

            return root;
        }

        public async Task Install(
            PluginPackage package, IReadOnlyList<PluginPermission>? granted = null, string tenant = Tenant)
        {
            var installed = await Runtime.InstallAsync(Admin(tenant), package, granted ?? Grants());
            Assert.True(installed.IsSuccess, installed.IsFailure ? installed.Error.Description : string.Empty);
        }

        public async Task Start(string key, string tenant = Tenant)
        {
            var loaded = await Runtime.Lifecycle.LoadAsync(Admin(tenant), key);
            Assert.True(loaded.IsSuccess, loaded.IsFailure ? loaded.Error.Description : string.Empty);

            var started = await Runtime.Lifecycle.StartAsync(Admin(tenant), key);
            Assert.True(started.IsSuccess, started.IsFailure ? started.Error.Description : string.Empty);
        }

        public void Dispose()
        {
            Provider.Dispose();

            foreach (var directory in _directories.Where(Directory.Exists))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
