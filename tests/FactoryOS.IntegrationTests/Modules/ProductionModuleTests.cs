using System.Collections.Concurrent;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Production;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// Proves the Production module works event-driven through the real in-process event bus: a released order and
/// its counts, published on the bus, are consumed by the plugin's handlers, which publish a
/// <see cref="ProductionOrderCompleted"/> back once the target is reached — no module referencing another, only
/// the bus.
/// </summary>
public sealed class ProductionModuleTests
{
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

    [Fact]
    public async Task A_released_order_reaching_its_target_yields_a_completed_event()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();
        new ProductionPlugin().ConfigureServices(services);

        var sink = new CaptureSink();
        services.AddSingleton(sink);
        services.AddScoped<IEventHandler<ProductionOrderCompleted>, CapturingHandler<ProductionOrderCompleted>>();

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();

        await bus.PublishAsync(new ProductionOrderReleased
        {
            Tenant = "acme",
            OrderId = "PO-1",
            ProductId = "widget",
            TargetQuantity = 100,
            ReleasedAt = DateTimeOffset.UnixEpoch,
        });
        await bus.PublishAsync(new ProductionCountReported
        {
            Tenant = "acme",
            OrderId = "PO-1",
            ProducedCount = 60,
            ReportedAt = DateTimeOffset.UnixEpoch.AddHours(1),
        });

        Assert.Empty(sink.Events.OfType<ProductionOrderCompleted>()); // not yet

        await bus.PublishAsync(new ProductionCountReported
        {
            Tenant = "acme",
            OrderId = "PO-1",
            ProducedCount = 40,
            ReportedAt = DateTimeOffset.UnixEpoch.AddHours(2),
        });

        var done = Assert.Single(sink.Events.OfType<ProductionOrderCompleted>());
        Assert.Equal("PO-1", done.OrderId);
        Assert.Equal(100, done.TotalProduced);
    }
}
