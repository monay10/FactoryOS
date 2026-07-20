using System.Net.Http;
using FactoryOS.Connectors.Webhook.Application;
using FactoryOS.Connectors.Webhook.Domain;
using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FactoryOS.Connectors.Webhook;

/// <summary>
/// The webhook transport connector as a plugin — the second outbound connector, delivering over HTTP. It
/// registers the connector, its HTTP sender, and the bus bridge that turns <see cref="NotificationDispatched"/>
/// on the <c>webhook</c> transport into a POST and a <see cref="NotificationDelivered"/> fact. It runs happily
/// alongside the log connector: each bridge drives only its own transport. Installing or removing this folder
/// adds or removes the webhook transport with zero core changes.
/// </summary>
public sealed class WebhookConnectorPlugin : PluginBase
{
    /// <summary>The plugin key, matching <c>connector.json</c>.</summary>
    public const string PluginKey = "connector.webhook";

    /// <inheritdoc />
    public override string Key => PluginKey;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new WebhookConnectorOptions());
        services.TryAddSingleton<IProcessedEventLog, InMemoryProcessedEventLog>();
        services.TryAddSingleton(static _ => new HttpClient());
        services.TryAddSingleton<IWebhookSender>(static sp => new HttpWebhookSender(sp.GetRequiredService<HttpClient>()));
        services.TryAddSingleton(static sp =>
            new WebhookConnector(sp.GetRequiredService<IWebhookSender>(), sp.GetRequiredService<WebhookConnectorOptions>()));

        services.AddScoped<IEventHandler<NotificationDispatched>, NotificationDispatchedHandler>();
    }
}
