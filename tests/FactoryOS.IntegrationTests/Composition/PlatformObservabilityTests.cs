using FactoryOS.Api.Platform;
using FactoryOS.Plugins.Runtime.Domain;
using FactoryOS.Plugins.Runtime.Execution;
using FactoryOS.Plugins.Runtime.Integration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FactoryOS.Plugins.Workflow.Audit.Domain;
using FactoryOS.Plugins.Workflow.Audit.Execution;
using MonitoringEngine = FactoryOS.Plugins.Workflow.Monitoring.Execution.MonitoringEngine;

namespace FactoryOS.IntegrationTests.Composition;

/// <summary>
/// Exercises the read-only <c>/platform/*</c> observability projections against the real composed engines. These
/// are the pure functions the endpoints delegate to; testing them here proves tenant scoping and projection
/// without standing up the HTTP pipeline.
/// </summary>
public sealed class PlatformObservabilityTests
{
    private const string Tenant = "acme";

    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddPlatformEngines(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }

    [Fact]
    public void A_tenant_with_nothing_installed_lists_no_plugins_and_no_extensions()
    {
        using var provider = Build();
        var runtime = provider.GetRequiredService<IPluginRuntime>();

        Assert.Empty(PlatformObservability.Plugins(runtime, Tenant));
        Assert.Empty(PlatformObservability.Extensions(runtime, Tenant, PluginExtensionPointKind.Connector));
    }

    [Fact]
    public void The_audit_trail_projects_a_tenant_s_records_newest_first()
    {
        using var provider = Build();
        var audit = provider.GetRequiredService<AuditEngine>();

        audit.Record(AuditEntries.PluginOperation(Tenant, "alpha", "Installed", AuditActor.User("ops")));
        audit.Record(AuditEntries.PluginOperation(Tenant, "beta", "Started", AuditActor.User("ops")));

        var report = PlatformObservability.Audit(audit, Tenant);

        Assert.True(report.ChainValid);
        Assert.Equal(2, report.Records.Count);
        // Newest first: the second record recorded is the first returned.
        Assert.True(report.Records[0].Sequence > report.Records[1].Sequence);
        Assert.Equal("ops", report.Records[0].Actor);
    }

    [Fact]
    public void The_audit_trail_never_reads_across_tenants()
    {
        using var provider = Build();
        var audit = provider.GetRequiredService<AuditEngine>();

        audit.Record(AuditEntries.PluginOperation(Tenant, "alpha", "Installed", AuditActor.User("ops")));

        Assert.Empty(PlatformObservability.Audit(audit, "other-factory").Records);
    }

    [Fact]
    public void The_audit_export_renders_a_tenant_s_trail_with_the_verifiable_hashes()
    {
        using var provider = Build();
        var audit = provider.GetRequiredService<AuditEngine>();
        audit.Record(AuditEntries.PluginOperation(Tenant, "alpha", "Installed", AuditActor.User("ops")));
        audit.Record(AuditEntries.PluginOperation("other-factory", "beta", "Installed", AuditActor.User("ops")));

        var head = audit.ListByTenant(Tenant).Single();
        var csv = audit.Export(new AuditQuery { Tenant = Tenant }, AuditExportFormat.Csv, "ops");
        var json = audit.Export(new AuditQuery { Tenant = Tenant }, AuditExportFormat.Json, "ops");

        // The CSV carries the header and only this tenant's record — never the other factory's.
        Assert.StartsWith("Sequence,Tenant,OccurredOnUtc", csv);
        Assert.Contains(head.Hash, csv, StringComparison.Ordinal);
        Assert.DoesNotContain("other-factory", csv, StringComparison.Ordinal);
        // The hash travels in both renderings, so the recipient can verify the chain independently.
        Assert.Contains(head.Hash, json, StringComparison.Ordinal);
    }

    [Fact]
    public void The_audit_search_filters_by_actor_and_returns_newest_first()
    {
        using var provider = Build();
        var audit = provider.GetRequiredService<AuditEngine>();
        audit.Record(AuditEntries.PluginOperation(Tenant, "alpha", "Installed", AuditActor.User("ops")));
        audit.Record(AuditEntries.PluginOperation(Tenant, "beta", "Started", AuditActor.User("admin")));
        audit.Record(AuditEntries.PluginOperation(Tenant, "gamma", "Stopped", AuditActor.User("ops")));

        var parse = PlatformObservability.ParseAuditQuery(
            Tenant, null, null, null, null, actor: "ops", null, null, null, null);
        Assert.True(parse.Ok);
        var view = PlatformObservability.SearchAudit(audit, parse.Query!);

        Assert.Equal(2, view.Count);
        Assert.All(view.Records, record => Assert.Equal("ops", record.Actor));
        Assert.True(view.Records[0].Sequence > view.Records[1].Sequence);
    }

    [Fact]
    public void The_audit_search_never_reads_across_tenants()
    {
        using var provider = Build();
        var audit = provider.GetRequiredService<AuditEngine>();
        audit.Record(AuditEntries.PluginOperation(Tenant, "alpha", "Installed", AuditActor.User("ops")));

        var parse = PlatformObservability.ParseAuditQuery(
            "other-factory", null, null, null, null, null, null, null, null, null);

        Assert.Empty(PlatformObservability.SearchAudit(audit, parse.Query!).Records);
    }

    [Fact]
    public void An_unrecognised_audit_filter_is_rejected_rather_than_ignored()
    {
        var parse = PlatformObservability.ParseAuditQuery(
            Tenant, category: "NotACategory", null, null, null, null, null, null, null, null);

        Assert.False(parse.Ok);
        Assert.Contains("AuditCategory", parse.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void The_audit_search_limit_is_clamped_to_the_maximum()
    {
        var parse = PlatformObservability.ParseAuditQuery(
            Tenant, null, null, null, null, null, null, null, null, limit: 100_000);

        Assert.True(parse.Ok);
        Assert.Equal(PlatformObservability.MaxAuditSearchLimit, parse.Query!.Limit);
    }

    [Fact]
    public void Metrics_lists_the_plugin_runtime_definitions_once_the_metric_sink_is_live()
    {
        using var provider = Build();
        // Resolving the metric sink registers the plugin metric series with the monitoring engine.
        _ = provider.GetRequiredService<IPluginMetricSink>();
        var monitoring = provider.GetRequiredService<MonitoringEngine>();

        var report = PlatformObservability.Metrics(monitoring);

        Assert.Contains(report.Definitions, definition => definition.Key == "plugin.installs");
    }
}
