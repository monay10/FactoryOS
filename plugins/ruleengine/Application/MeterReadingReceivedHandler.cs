using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.RuleEngine.Domain;

namespace FactoryOS.Plugins.RuleEngine.Application;

/// <summary>
/// The Rule Engine's consumer of <see cref="MeterReadingReceived"/>. For every configured rule that watches the
/// reading's metric and whose comparison matches, it emits a <see cref="RuleTriggered"/> — once per (rule,
/// reading) pair, so a redelivered reading re-fires nothing. It references the shared events and the pure
/// evaluator only, never any consuming module: rules turn observations into normalized action requests, and who
/// acts on them is entirely decoupled.
/// </summary>
public sealed class MeterReadingReceivedHandler : IEventHandler<MeterReadingReceived>
{
    private readonly IEventBus _bus;
    private readonly RuleEngineOptions _options;
    private readonly IRuleFiringLog _firingLog;

    /// <summary>Initializes a new instance of the <see cref="MeterReadingReceivedHandler"/> class.</summary>
    /// <param name="bus">The event bus to announce matched rules on.</param>
    /// <param name="options">The module options carrying the rules.</param>
    /// <param name="firingLog">The per-(rule, reading) firing log for idempotency.</param>
    public MeterReadingReceivedHandler(IEventBus bus, RuleEngineOptions options, IRuleFiringLog firingLog)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(firingLog);
        _bus = bus;
        _options = options;
        _firingLog = firingLog;
    }

    /// <inheritdoc />
    public async Task HandleAsync(MeterReadingReceived integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var reading = integrationEvent.Reading;

        foreach (var rule in _options.Rules)
        {
            if (!string.Equals(rule.Metric, reading.Metric, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!RuleEvaluator.Matches(rule.Operator, reading.Value, rule.Threshold))
            {
                continue;
            }

            if (!_firingLog.TryMarkFired(rule.Id, integrationEvent.EventId))
            {
                continue;
            }

            await _bus.PublishAsync(
                new RuleTriggered
                {
                    Tenant = reading.Tenant,
                    RuleId = rule.Id,
                    Metric = reading.Metric,
                    MeterId = reading.MeterId,
                    Value = reading.Value,
                    Operator = rule.Operator.ToString(),
                    Threshold = rule.Threshold,
                    Action = rule.Action,
                    TriggeredAt = reading.ReadingAt,
                    SourceEventId = integrationEvent.EventId,
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
