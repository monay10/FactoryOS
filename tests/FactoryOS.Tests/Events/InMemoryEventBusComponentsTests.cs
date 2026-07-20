using FactoryOS.Contracts.Events;
using FactoryOS.EventBus.InProcess;

namespace FactoryOS.Tests.Events;

public sealed class InMemoryEventBusComponentsTests
{
    private sealed record SampleEvent : IntegrationEvent;

    [Fact]
    public async Task Dead_letter_queue_stores_enqueued_messages()
    {
        var queue = new InMemoryDeadLetterQueue();
        var envelope = new DeadLetterEnvelope(
            Guid.NewGuid(),
            Guid.NewGuid(),
            nameof(SampleEvent),
            Guid.NewGuid(),
            null,
            "trace",
            EventPriority.Normal,
            3,
            "failure",
            new SampleEvent(),
            DateTimeOffset.UtcNow);

        await queue.EnqueueAsync(envelope);

        Assert.Same(envelope, Assert.Single(queue.Messages));
    }

    [Fact]
    public void Metrics_accumulate_each_counter_independently()
    {
        var metrics = new InMemoryEventBusMetrics();

        metrics.RecordPublished(nameof(SampleEvent), EventPriority.High);
        metrics.RecordHandled(nameof(SampleEvent));
        metrics.RecordRetry(nameof(SampleEvent));
        metrics.RecordRetry(nameof(SampleEvent));
        metrics.RecordDeadLettered(nameof(SampleEvent));

        Assert.Equal(1, metrics.Published);
        Assert.Equal(1, metrics.Handled);
        Assert.Equal(2, metrics.Retried);
        Assert.Equal(1, metrics.DeadLettered);
    }
}
