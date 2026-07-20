using System.Collections.Concurrent;
using FactoryOS.Connectors.Log;
using FactoryOS.Connectors.Log.Domain;
using FactoryOS.Connectors.Webhook;
using FactoryOS.Connectors.Webhook.Domain;
using FactoryOS.Contracts.Connectors;
using FactoryOS.Contracts.Events;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// Two outbound connectors — log and webhook — installed side by side, multiplexed by transport over the real
/// bus. A dispatch on the <c>webhook</c> transport is POSTed by the webhook connector only; a dispatch on the
/// <c>log</c> transport is journaled by the log connector only. Each bridge drives its own transport and ignores
/// the other's, so transports compose without a central router.
/// </summary>
public sealed class OutboundTransportMultiplexTests
{
    private sealed class RecordingWebhookSender : IWebhookSender
    {
        public ConcurrentBag<OutboundMessage> Sent { get; } = [];

        public Task<OutboundResult> SendAsync(Uri endpoint, OutboundMessage message, CancellationToken cancellationToken)
        {
            Sent.Add(message);
            return Task.FromResult(OutboundResult.Ok("HTTP 200"));
        }
    }

    private sealed class CaptureSink
    {
        public ConcurrentBag<IIntegrationEvent> Events { get; } = [];
    }

    private sealed class CapturingHandler<TEvent> : IEventHandler<TEvent>
        where TEvent : IIntegrationEvent
    {
        private readonly CaptureSink _sink;

        public CapturingHandler(CaptureSink sink) => _sink = sink;

        public Task HandleAsync(TEvent integrationEvent, EventContext context, CancellationToken cancellationToken)
        {
            _sink.Events.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    private static NotificationDispatched Dispatched(string transport, string channel) => new()
    {
        Tenant = "acme",
        Channel = channel,
        Transport = transport,
        Priority = "Critical",
        Subject = $"{transport} subject",
        Action = "Notify",
        DispatchedAt = DateTimeOffset.UnixEpoch,
    };

    [Fact]
    public async Task Each_transport_is_handled_by_its_own_connector_only()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();

        var sender = new RecordingWebhookSender();
        services.AddSingleton<IWebhookSender>(sender); // registered first, so the plugin's HTTP sender is not used
        services.AddSingleton(new WebhookConnectorOptions
        {
            ChannelUrls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["ops"] = "https://hooks.example.com/ops" },
        });
        new LogConnectorPlugin().ConfigureServices(services);
        new WebhookConnectorPlugin().ConfigureServices(services);

        var sink = new CaptureSink();
        services.AddSingleton(sink);
        services.AddScoped<IEventHandler<NotificationDelivered>, CapturingHandler<NotificationDelivered>>();

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();
        var journal = provider.GetRequiredService<IDeliveryJournal>();

        await bus.PublishAsync(Dispatched("webhook", "ops"));
        await bus.PublishAsync(Dispatched("log", "quality"));

        // Webhook connector delivered only the webhook dispatch.
        var posted = Assert.Single(sender.Sent);
        Assert.Equal("ops", posted.Channel);

        // Log connector journaled only the log dispatch.
        var journaled = Assert.Single(journal.ForTenant("acme"));
        Assert.Equal("quality", journaled.Channel);

        // Two deliveries announced, one per transport.
        var delivered = sink.Events.OfType<NotificationDelivered>().ToList();
        Assert.Equal(2, delivered.Count);
        Assert.Contains(delivered, d => d.Transport == "webhook");
        Assert.Contains(delivered, d => d.Transport == "log");
    }
}
