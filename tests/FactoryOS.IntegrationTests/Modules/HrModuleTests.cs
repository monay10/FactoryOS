using System.Collections.Concurrent;
using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Hr;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryOS.IntegrationTests.Modules;

/// <summary>
/// Proves the HR module works event-driven through the real in-process event bus: a certification and a shift
/// staffing, published on the bus, are consumed by the plugin's handlers, which publish a
/// <see cref="CertificationGapDetected"/> when the certification is expired at the shift start — no module
/// referencing another, only the bus.
/// </summary>
public sealed class HrModuleTests
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
    public async Task An_expired_certification_on_a_staffed_shift_yields_a_gap()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventBus();
        new HrPlugin().ConfigureServices(services);

        var sink = new CaptureSink();
        services.AddSingleton(sink);
        services.AddScoped<IEventHandler<CertificationGapDetected>, CapturingHandler<CertificationGapDetected>>();

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IEventBus>();

        var shiftStart = DateTimeOffset.UnixEpoch.AddDays(100);

        await bus.PublishAsync(new WorkerCertificationRecorded
        {
            Tenant = "acme",
            WorkerId = "w-1",
            Certification = "Forklift",
            ExpiresAt = shiftStart.AddDays(-2), // expired before the shift
        });
        await bus.PublishAsync(new ShiftStaffed
        {
            Tenant = "acme",
            ShiftId = "s-1",
            WorkerId = "w-1",
            RequiredCertification = "Forklift",
            ShiftStart = shiftStart,
        });

        var gap = Assert.Single(sink.Events.OfType<CertificationGapDetected>());
        Assert.Equal("Expired", gap.Reason);
        Assert.Equal("w-1", gap.WorkerId);
    }
}
