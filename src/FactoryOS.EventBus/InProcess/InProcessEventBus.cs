using System.Diagnostics;
using FactoryOS.Contracts.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactoryOS.EventBus.InProcess;

/// <summary>
/// Default in-process implementation of <see cref="IEventBus"/>. It resolves handlers from a fresh
/// DI scope, applies per-handler retry with exponential back-off, dead-letters exhausted messages and
/// records metrics — flowing correlation and trace identifiers through the delivery context so a
/// single failing handler never blocks the others.
/// </summary>
public sealed class InProcessEventBus : IEventBus
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDeadLetterQueue _deadLetterQueue;
    private readonly IEventBusMetrics _metrics;
    private readonly ILogger<InProcessEventBus> _logger;
    private readonly EventBusOptions _options;

    /// <summary>Initializes a new instance of the <see cref="InProcessEventBus"/> class.</summary>
    /// <param name="scopeFactory">Factory used to create a DI scope per publish.</param>
    /// <param name="deadLetterQueue">The queue that receives exhausted messages.</param>
    /// <param name="metrics">The metrics recorder.</param>
    /// <param name="options">The strongly-typed event-bus options.</param>
    /// <param name="logger">The logger.</param>
    public InProcessEventBus(
        IServiceScopeFactory scopeFactory,
        IDeadLetterQueue deadLetterQueue,
        IEventBusMetrics metrics,
        IOptions<EventBusOptions> options,
        ILogger<InProcessEventBus> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(deadLetterQueue);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _scopeFactory = scopeFactory;
        _deadLetterQueue = deadLetterQueue;
        _metrics = metrics;
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(
        TEvent integrationEvent,
        EventPublishOptions? options = null,
        CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var publishOptions = options ?? EventPublishOptions.Default;
        var eventType = typeof(TEvent).Name;
        var messageId = Guid.CreateVersion7();
        var correlationId = publishOptions.CorrelationId ?? Guid.CreateVersion7();
        var traceId = Activity.Current?.TraceId.ToString() ?? messageId.ToString("N");

        _metrics.RecordPublished(eventType, publishOptions.Priority);

        var scopeState = new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["TraceId"] = traceId,
            ["EventType"] = eventType,
            ["Priority"] = publishOptions.Priority,
        };

        using var loggingScope = _logger.BeginScope(scopeState);
        using var serviceScope = _scopeFactory.CreateScope();

        var handlers = serviceScope.ServiceProvider.GetServices<IEventHandler<TEvent>>().ToArray();
        if (handlers.Length == 0)
        {
            EventBusLog.NoHandlers(_logger, eventType);
            return;
        }

        foreach (var handler in handlers)
        {
            await DispatchAsync(
                    handler, integrationEvent, messageId, correlationId, traceId, eventType, publishOptions, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The event bus must isolate handler failures to apply retry and dead-lettering.")]
    private async Task DispatchAsync<TEvent>(
        IEventHandler<TEvent> handler,
        TEvent integrationEvent,
        Guid messageId,
        Guid correlationId,
        string traceId,
        string eventType,
        EventPublishOptions publishOptions,
        CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        var policy = _options.ToRetryPolicy();

        for (var attempt = 1; attempt <= policy.MaxAttempts; attempt++)
        {
            var context = new EventContext(
                messageId,
                integrationEvent.EventId,
                correlationId,
                publishOptions.CausationId,
                traceId,
                publishOptions.Priority,
                attempt,
                integrationEvent.OccurredOnUtc);

            try
            {
                await handler.HandleAsync(integrationEvent, context, cancellationToken).ConfigureAwait(false);
                _metrics.RecordHandled(eventType);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                if (attempt < policy.MaxAttempts)
                {
                    _metrics.RecordRetry(eventType);
                    EventBusLog.HandlerFailedRetrying(
                        _logger, exception, handler.GetType().Name, eventType, attempt, policy.MaxAttempts);

                    await Task.Delay(policy.GetDelay(attempt), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                _metrics.RecordDeadLettered(eventType);
                EventBusLog.HandlerExhausted(
                    _logger, exception, handler.GetType().Name, policy.MaxAttempts, eventType);

                var deadLetter = new DeadLetterEnvelope(
                    messageId,
                    integrationEvent.EventId,
                    eventType,
                    correlationId,
                    publishOptions.CausationId,
                    traceId,
                    publishOptions.Priority,
                    attempt,
                    exception.Message,
                    integrationEvent,
                    DateTimeOffset.UtcNow);

                await _deadLetterQueue.EnqueueAsync(deadLetter, cancellationToken).ConfigureAwait(false);
                return;
            }
        }
    }
}
