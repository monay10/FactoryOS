using FactoryOS.Api.Composition;
using FactoryOS.Plugins.Runtime.Execution;
using FactoryOS.Plugins.Runtime.Integration;
using FactoryOS.Plugins.Runtime.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ApprovalEngine = FactoryOS.Plugins.Workflow.Approvals.Execution.ApprovalEngine;
using AuditEngine = FactoryOS.Plugins.Workflow.Audit.Execution.AuditEngine;
using ConnectorEngine = FactoryOS.Connectors.Runtime.Execution.ConnectorEngine;
using FormEngine = FactoryOS.Plugins.Forms.Engine.Execution.FormEngine;
using HumanTaskEngine = FactoryOS.Plugins.Workflow.Tasks.Execution.HumanTaskEngine;
using MonitoringEngine = FactoryOS.Plugins.Workflow.Monitoring.Execution.MonitoringEngine;
using NotificationEngine = FactoryOS.Plugins.Workflow.Notifications.Execution.NotificationEngine;
using SecurityEngine = FactoryOS.Plugins.Workflow.Security.Execution.SecurityEngine;
using SlaEngine = FactoryOS.Plugins.Workflow.SLA.Execution.SlaEngine;
using WorkflowStateEngine = FactoryOS.Plugins.Workflow.Engine.Execution.WorkflowEngine;

namespace FactoryOS.IntegrationTests.Composition;

/// <summary>
/// Verifies that <c>AddPlatformEngines</c> — the composition root the running host calls — actually stands every
/// platform engine and both platform runtimes up in one validated container, and binds the plugin runtime's
/// ports to the engines. This is the proof the engines are live in the process, not merely present as code.
/// </summary>
public sealed class PlatformCompositionTests
{
    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();

        services.AddPlatformEngines(configuration);

        // ValidateOnBuild is the whole point: if any engine or runtime cannot be constructed from the container
        // the host would compose, this throws here rather than at the first request.
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }

    [Fact]
    public void Every_platform_engine_resolves_from_the_composed_container()
    {
        using var provider = Build();

        Assert.NotNull(provider.GetRequiredService<SecurityEngine>());
        Assert.NotNull(provider.GetRequiredService<AuditEngine>());
        Assert.NotNull(provider.GetRequiredService<MonitoringEngine>());
        Assert.NotNull(provider.GetRequiredService<ApprovalEngine>());
        Assert.NotNull(provider.GetRequiredService<SlaEngine>());
        Assert.NotNull(provider.GetRequiredService<NotificationEngine>());
        Assert.NotNull(provider.GetRequiredService<HumanTaskEngine>());
        Assert.NotNull(provider.GetRequiredService<FormEngine>());
        Assert.NotNull(provider.GetRequiredService<WorkflowStateEngine>());
    }

    [Fact]
    public void Both_platform_runtimes_resolve_from_the_composed_container()
    {
        using var provider = Build();

        Assert.NotNull(provider.GetRequiredService<ConnectorEngine>());
        Assert.NotNull(provider.GetRequiredService<IPluginRuntime>());
        Assert.NotNull(provider.GetRequiredService<PluginEngine>());
    }

    [Fact]
    public void The_plugin_runtime_ports_are_bound_to_the_platform_engines_not_the_in_memory_defaults()
    {
        using var provider = Build();

        // The engine-backed adapters were registered before AddPluginRuntime, so its in-memory defaults deferred.
        Assert.IsType<SecurityEnginePluginAuthorizer>(provider.GetRequiredService<IPluginAuthorizer>());
        Assert.IsType<AuditEnginePluginSink>(provider.GetRequiredService<IPluginAuditSink>());
        Assert.IsType<MonitoringEnginePluginSink>(provider.GetRequiredService<IPluginMetricSink>());
    }

    [Fact]
    public void The_platform_status_reports_every_component_live()
    {
        using var provider = Build();

        var components = PlatformStatus.Describe(provider);

        Assert.Equal(11, components.Count);
        Assert.All(components, component => Assert.True(component.Up, $"{component.Name} is not live."));
    }

    [Fact]
    public async Task The_platform_health_check_is_healthy_when_everything_is_composed()
    {
        using var provider = Build();
        var check = new PlatformStatusHealthCheck(provider);

        var result = await check.CheckHealthAsync(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext());

        Assert.Equal(
            Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy, result.Status);
    }
}
