using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.Hr;
using FactoryOS.Plugins.Hr.Application;
using FactoryOS.Plugins.Hr.Domain;

namespace FactoryOS.Tests.Hr;

public sealed class ShiftStaffedHandlerTests
{
    private sealed class RecordingEventBus : IEventBus
    {
        public List<IIntegrationEvent> Published { get; } = [];

        public Task PublishAsync<TEvent>(TEvent integrationEvent, EventPublishOptions? options = null, CancellationToken cancellationToken = default)
            where TEvent : IIntegrationEvent
        {
            Published.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    private static readonly DateTimeOffset ShiftStart = DateTimeOffset.UnixEpoch.AddDays(100);

    private sealed record Harness(
        WorkerCertificationRecordedHandler Cert,
        ShiftStaffedHandler Shift,
        RecordingEventBus Bus);

    private static Harness Build(bool treatMissingAsGap = true)
    {
        var bus = new RecordingEventBus();
        var registry = new InMemoryCertificationRegistry();
        var options = new HrOptions { TreatMissingAsGap = treatMissingAsGap };
        return new Harness(
            new WorkerCertificationRecordedHandler(registry),
            new ShiftStaffedHandler(bus, registry, new InMemoryProcessedEventLog(), options),
            bus);
    }

    private static EventContext Context(IIntegrationEvent e) =>
        new(Guid.NewGuid(), e.EventId, Guid.NewGuid(), null, "trace", EventPriority.Normal, 1, e.OccurredOnUtc);

    private static WorkerCertificationRecorded Cert(DateTimeOffset expiry, string cert = "Forklift") => new()
    {
        Tenant = "acme",
        WorkerId = "w-1",
        Certification = cert,
        ExpiresAt = expiry,
    };

    private static ShiftStaffed Shift(string required = "Forklift") => new()
    {
        Tenant = "acme",
        ShiftId = "s-1",
        WorkerId = "w-1",
        RequiredCertification = required,
        ShiftStart = ShiftStart,
    };

    private static async Task Record(Harness h, WorkerCertificationRecorded c) =>
        await h.Cert.HandleAsync(c, Context(c), CancellationToken.None);

    private static async Task Staff(Harness h, ShiftStaffed s) =>
        await h.Shift.HandleAsync(s, Context(s), CancellationToken.None);

    [Fact]
    public async Task A_valid_certification_raises_no_gap()
    {
        var h = Build();
        await Record(h, Cert(ShiftStart.AddDays(30)));

        await Staff(h, Shift());

        Assert.Empty(h.Bus.Published);
    }

    [Fact]
    public async Task An_expired_certification_raises_an_expired_gap()
    {
        var h = Build();
        await Record(h, Cert(ShiftStart.AddDays(-1)));

        var shift = Shift();
        await Staff(h, shift);

        var gap = Assert.Single(h.Bus.Published.OfType<CertificationGapDetected>());
        Assert.Equal("Expired", gap.Reason);
        Assert.Equal("Forklift", gap.RequiredCertification);
        Assert.Equal(ShiftStart.AddDays(-1), gap.ExpiresAt);
        Assert.Equal(shift.EventId, gap.SourceEventId);
    }

    [Fact]
    public async Task A_worker_without_the_certification_raises_a_missing_gap()
    {
        var h = Build();

        await Staff(h, Shift());

        var gap = Assert.Single(h.Bus.Published.OfType<CertificationGapDetected>());
        Assert.Equal("Missing", gap.Reason);
        Assert.Null(gap.ExpiresAt);
    }

    [Fact]
    public async Task A_shift_without_a_requirement_is_skipped()
    {
        var h = Build();

        await Staff(h, Shift(required: ""));

        Assert.Empty(h.Bus.Published);
    }

    [Fact]
    public async Task Redelivery_of_the_same_staffing_raises_only_one_gap()
    {
        var h = Build();
        var shift = Shift();

        await Staff(h, shift);
        await Staff(h, shift); // same event id

        Assert.Single(h.Bus.Published.OfType<CertificationGapDetected>());
    }
}
