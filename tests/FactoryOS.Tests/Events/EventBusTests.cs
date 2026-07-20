using FactoryOS.Contracts.Events;
using FactoryOS.EventBus.InProcess;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FactoryOS.Tests.Events;

public sealed class EventBusTests
{
    private sealed record PingEvent(string Message) : IntegrationEvent;

    private sealed class Recorder
    {
        private readonly List<EventContext> _contexts = [];

        public IReadOnlyList<EventContext> Contexts => _contexts;

        public int Count => _contexts.Count;

        public void Record(EventContext context)
        {
            _contexts.Add(context);
        }
    }

    private sealed class PingHandler : IEventHandler<PingEvent>
    {
        private readonly Recorder _recorder;

        public PingHandler(Recorder recorder)
        {
            _recorder = recorder;
        }

        public Task HandleAsync(PingEvent integrationEvent, EventContext context, CancellationToken cancellationToken)
        {
            _recorder.Record(context);
            return Task.CompletedTask;
        }
    }

    private sealed class SecondPingHandler : IEventHandler<PingEvent>
    {
        private readonly Recorder _recorder;

        public SecondPingHandler(Recorder recorder)
        {
            _recorder = recorder;
        }

        public Task HandleAsync(PingEvent integrationEvent, EventContext context, CancellationToken cancellationToken)
        {
            _recorder.Record(context);
            return Task.CompletedTask;
        }
    }

    private sealed class AlwaysFailingHandler : IEventHandler<PingEvent>
    {
        public Task HandleAsync(PingEvent integrationEvent, EventContext context, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("boom");
        }
    }

    private sealed class FailOnceHandler : IEventHandler<PingEvent>
    {
        private readonly Recorder _recorder;
        private int _attempts;

        public FailOnceHandler(Recorder recorder)
        {
            _recorder = recorder;
        }

        public Task HandleAsync(PingEvent integrationEvent, EventContext context, CancellationToken cancellationToken)
        {
            _attempts++;
            if (_attempts == 1)
            {
                throw new InvalidOperationException("transient");
            }

            _recorder.Record(context);
            return Task.CompletedTask;
        }
    }

    private static ServiceProvider BuildProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddEventBus();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.Configure<EventBusOptions>(options => options.RetryBaseDelayMilliseconds = 0);
        services.AddSingleton<Recorder>();
        configure(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Publishing_invokes_the_registered_handler()
    {
        using var provider = BuildProvider(services =>
            services.AddScoped<IEventHandler<PingEvent>, PingHandler>());
        var bus = provider.GetRequiredService<IEventBus>();

        await bus.PublishAsync(new PingEvent("hello"));

        Assert.Equal(1, provider.GetRequiredService<Recorder>().Count);
    }

    [Fact]
    public async Task Handler_receives_correlation_priority_and_attempt_metadata()
    {
        using var provider = BuildProvider(services =>
            services.AddScoped<IEventHandler<PingEvent>, PingHandler>());
        var bus = provider.GetRequiredService<IEventBus>();
        var correlationId = Guid.NewGuid();

        await bus.PublishAsync(
            new PingEvent("x"),
            new EventPublishOptions { CorrelationId = correlationId, Priority = EventPriority.High });

        var context = Assert.Single(provider.GetRequiredService<Recorder>().Contexts);
        Assert.Equal(correlationId, context.CorrelationId);
        Assert.Equal(EventPriority.High, context.Priority);
        Assert.Equal(1, context.Attempt);
    }

    [Fact]
    public async Task All_registered_handlers_are_invoked()
    {
        using var provider = BuildProvider(services =>
        {
            services.AddScoped<IEventHandler<PingEvent>, PingHandler>();
            services.AddScoped<IEventHandler<PingEvent>, SecondPingHandler>();
        });
        var bus = provider.GetRequiredService<IEventBus>();

        await bus.PublishAsync(new PingEvent("x"));

        Assert.Equal(2, provider.GetRequiredService<Recorder>().Count);
    }

    [Fact]
    public async Task A_failing_handler_is_retried_then_dead_lettered()
    {
        using var provider = BuildProvider(services =>
            services.AddScoped<IEventHandler<PingEvent>, AlwaysFailingHandler>());
        var bus = provider.GetRequiredService<IEventBus>();

        await bus.PublishAsync(new PingEvent("x"));

        var deadLetterQueue = (InMemoryDeadLetterQueue)provider.GetRequiredService<IDeadLetterQueue>();
        var metrics = (InMemoryEventBusMetrics)provider.GetRequiredService<IEventBusMetrics>();

        var deadLetter = Assert.Single(deadLetterQueue.Messages);
        Assert.Equal(3, deadLetter.Attempts);
        Assert.Equal(nameof(PingEvent), deadLetter.EventType);
        Assert.Equal(2, metrics.Retried);
        Assert.Equal(1, metrics.DeadLettered);
    }

    [Fact]
    public async Task A_transient_failure_recovers_without_dead_lettering()
    {
        using var provider = BuildProvider(services =>
            services.AddScoped<IEventHandler<PingEvent>, FailOnceHandler>());
        var bus = provider.GetRequiredService<IEventBus>();

        await bus.PublishAsync(new PingEvent("x"));

        var deadLetterQueue = (InMemoryDeadLetterQueue)provider.GetRequiredService<IDeadLetterQueue>();
        var metrics = (InMemoryEventBusMetrics)provider.GetRequiredService<IEventBusMetrics>();

        Assert.Equal(1, provider.GetRequiredService<Recorder>().Count);
        Assert.Empty(deadLetterQueue.Messages);
        Assert.Equal(1, metrics.Retried);
        Assert.Equal(1, metrics.Handled);
    }

    [Fact]
    public async Task Publishing_without_handlers_does_not_throw()
    {
        using var provider = BuildProvider(_ => { });
        var bus = provider.GetRequiredService<IEventBus>();

        await bus.PublishAsync(new PingEvent("x"));

        var metrics = (InMemoryEventBusMetrics)provider.GetRequiredService<IEventBusMetrics>();
        Assert.Equal(1, metrics.Published);
        Assert.Equal(0, metrics.Handled);
    }

    [Fact]
    public async Task Publishing_a_null_event_throws()
    {
        using var provider = BuildProvider(_ => { });
        var bus = provider.GetRequiredService<IEventBus>();

        await Assert.ThrowsAsync<ArgumentNullException>(() => bus.PublishAsync<PingEvent>(null!));
    }
}
