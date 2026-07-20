using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Plugins.Hr.Application;
using FactoryOS.Plugins.Hr.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FactoryOS.Plugins.Hr;

/// <summary>
/// The HR plugin. It subscribes to <see cref="WorkerCertificationRecorded"/> and <see cref="ShiftStaffed"/> and
/// contributes its certification registry, idempotency log and handlers. Installing or removing this folder adds
/// or removes certification-gap checking with zero core changes — the plugin is self-contained and event-driven.
/// </summary>
public sealed class HrPlugin : PluginBase
{
    /// <summary>The plugin key, matching <c>module.json</c>.</summary>
    public const string PluginKey = "hr";

    /// <inheritdoc />
    public override string Key => PluginKey;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new HrOptions());
        services.TryAddSingleton<ICertificationRegistry, InMemoryCertificationRegistry>();
        services.TryAddSingleton<IProcessedEventLog, InMemoryProcessedEventLog>();
        services.AddScoped<IEventHandler<WorkerCertificationRecorded>, WorkerCertificationRecordedHandler>();
        services.AddScoped<IEventHandler<ShiftStaffed>, ShiftStaffedHandler>();
    }
}
