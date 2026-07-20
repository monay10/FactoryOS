using FactoryOS.Connectors.Log.Application;
using FactoryOS.Connectors.Log.Domain;
using FactoryOS.Contracts.Connectors;
using FactoryOS.Contracts.Events;
using FactoryOS.Contracts.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FactoryOS.Connectors.Log;

/// <summary>
/// The log transport connector as a plugin — connectors are plugins too, and this is the first <b>outbound</b>
/// one. It registers the connector as an <see cref="IOutboundConnector"/>, its journal, and the bus bridge that
/// turns <see cref="NotificationDispatched"/> into a delivery and a <see cref="NotificationDelivered"/> fact. It
/// references only the shared contracts; installing or removing this folder adds or removes the "log" transport
/// with zero core changes.
/// </summary>
public sealed class LogConnectorPlugin : PluginBase
{
    /// <summary>The plugin key, matching <c>connector.json</c>.</summary>
    public const string PluginKey = "connector.log";

    /// <inheritdoc />
    public override string Key => PluginKey;

    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new LogConnectorOptions());
        services.TryAddSingleton<IDeliveryJournal, InMemoryDeliveryJournal>();
        services.TryAddSingleton<IProcessedEventLog, InMemoryProcessedEventLog>();
        services.TryAddSingleton<IOutboundConnector>(static sp =>
            new LogTransportConnector(sp.GetRequiredService<IDeliveryJournal>(), sp.GetRequiredService<LogConnectorOptions>()));

        services.AddScoped<IEventHandler<NotificationDispatched>, NotificationDispatchedHandler>();
    }
}
