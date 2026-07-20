using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.Plugins;
using FactoryOS.Gateway.Endpoints;
using FactoryOS.Plugins.Notification.Api;
using FactoryOS.Plugins.Notification.Application;
using FactoryOS.Plugins.Notification.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FactoryOS.Plugins.Notification;

/// <summary>
/// The Notification plugin — the Platform-layer bridge from a requested action to a dispatched notification. It
/// subscribes to <see cref="WorkflowActionRequested"/>, <see cref="ReportGenerated"/> and
/// <see cref="BrainAnswered"/>, routes each source's channel to a transport by configuration, records the dispatch
/// in a per-tenant outbox and emits <see cref="NotificationDispatched"/>.
/// Delivery over a real transport is a connector's job (connectors are the only door out), so this module never
/// talks to email/SMS/chat itself. It references the shared events, never any module. Removing this folder
/// removes notification routing with zero core changes.
/// </summary>
public sealed class NotificationPlugin : PluginBase
{
    /// <summary>The plugin key, matching <c>module.json</c>.</summary>
    public const string PluginKey = "notification";

    /// <inheritdoc />
    public override string Key => PluginKey;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new NotificationOptions());
        services.TryAddSingleton<INotificationOutbox, InMemoryNotificationOutbox>();

        services.AddScoped<IEventHandler<WorkflowActionRequested>, WorkflowActionRequestedHandler>();
        services.AddScoped<IEventHandler<ReportGenerated>, ReportGeneratedHandler>();
        services.AddScoped<IEventHandler<BrainAnswered>, BrainAnsweredHandler>();

        services.AddSingleton<IModuleApi>(static sp => new NotificationApi(
            sp.GetRequiredService<INotificationOutbox>()));
    }
}
