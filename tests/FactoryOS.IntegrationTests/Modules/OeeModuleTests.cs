using System.Collections.Concurrent;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Oee;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// Proves the OEE module works event-driven through the real in-process event bus: a
/// <see cref="ProductionPeriodReported"/> published on the bus is consumed by the plugin's handler, which
/// publishes an <see cref="OeeCalculated"/> back — no module referencing another, only the bus.
/// </summary>
public sealed class OeeModuleTests
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
    public async Task A_reported_period_on_the_bus_yields_a_calculated_oee_event()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();
        new OeePlugin().ConfigureServices(services);

        var sink = new CaptureSink();
        services.AddSingleton(sink);
        services.AddScoped<IEventHandler<OeeCalculated>, CapturingHandler<OeeCalculated>>();

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();

        await bus.PublishAsync(new ProductionPeriodReported
        {
            Tenant = "acme",
            MachineId = "press-1",
            PeriodStart = DateTimeOffset.UnixEpoch,
            PeriodEnd = DateTimeOffset.UnixEpoch.AddHours(8),
            PlannedTimeSeconds = 100m,
            RunTimeSeconds = 90m,
            IdealCycleTimeSeconds = 1m,
            TotalCount = 72,
            GoodCount = 54,
        });

        var calc = Assert.Single(sink.Events.OfType<OeeCalculated>());
        Assert.Equal("press-1", calc.MachineId);
        Assert.Equal(0.54m, calc.Oee);
    }
}
