using System.Collections.Concurrent;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Quality;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// Proves the Quality module works event-driven through the real in-process event bus: a
/// <see cref="QualityInspectionRecorded"/> published on the bus is consumed by the plugin's handler, which
/// publishes a <see cref="QualityAlertRaised"/> back once the rolling defect rate breaches the threshold — no
/// module referencing another, only the bus.
/// </summary>
public sealed class QualityModuleTests
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
    public async Task A_defective_inspection_on_the_bus_yields_a_quality_alert()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();
        new QualityPlugin().ConfigureServices(services);

        var sink = new CaptureSink();
        services.AddSingleton(sink);
        services.AddScoped<IEventHandler<QualityAlertRaised>, CapturingHandler<QualityAlertRaised>>();

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();

        await bus.PublishAsync(new QualityInspectionRecorded
        {
            Tenant = "acme",
            LineId = "line-1",
            ProductId = "widget",
            InspectedUnits = 100,
            DefectiveUnits = 8, // 8% > 5%
            InspectedAt = DateTimeOffset.UnixEpoch,
        });

        var alert = Assert.Single(sink.Events.OfType<QualityAlertRaised>());
        Assert.Equal("line-1", alert.LineId);
        Assert.Equal(0.08m, alert.DefectRate);
    }
}
