using FactoryOS.Contracts.Events;
using FactoryOS.Plugins.RuleEngine.Domain;

namespace FactoryOS.Plugins.RuleEngine.Application;

/// <summary>
/// The Rule Engine's third signal source: it evaluates the same declarative rules against computed
/// <see cref="CarbonEmissionCalculated"/> facts, treating the per-reading emission as the Standard Model metric
/// <c>CarbonCo2e</c> (in kg CO₂e) and the emitting source as the "meter". A rule such as
/// <c>CarbonCo2e GreaterThan 50 → RaiseSustainabilityAlert</c> thus fires on a high-emission reading exactly as a
/// temperature rule fires on an over-temp reading — one rule vocabulary over a third signal stream. It emits a
/// <see cref="RuleTriggered"/> once per (rule, emission event) pair, references the shared events and the pure
/// evaluator only, and never the Carbon module or any consuming module.
/// </summary>
public sealed class CarbonEmissionCalculatedHandler : IEventHandler<CarbonEmissionCalculated>
{
    /// <summary>The Standard Model metric name a computed emission is matched against, case-insensitively.</summary>
    public const string CarbonMetric = "CarbonCo2e";

    private readonly IEventBus _bus;
    private readonly RuleEngineOptions _options;
    private readonly IRuleFiringLog _firingLog;

    /// <summary>Initializes a new instance of the <see cref="CarbonEmissionCalculatedHandler"/> class.</summary>
    /// <param name="bus">The event bus to announce matched rules on.</param>
    /// <param name="options">The module options carrying the rules.</param>
    /// <param name="firingLog">The per-(rule, event) firing log for idempotency.</param>
    public CarbonEmissionCalculatedHandler(IEventBus bus, RuleEngineOptions options, IRuleFiringLog firingLog)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(firingLog);
        _bus = bus;
        _options = options;
        _firingLog = firingLog;
    }

    /// <inheritdoc />
    public async Task HandleAsync(CarbonEmissionCalculated integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        foreach (var rule in _options.Rules)
        {
            if (!string.Equals(rule.Metric, CarbonMetric, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!RuleEvaluator.Matches(rule.Operator, integrationEvent.Co2eKg, rule.Threshold))
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
                    Tenant = integrationEvent.Tenant,
                    RuleId = rule.Id,
                    Metric = CarbonMetric,
                    MeterId = integrationEvent.Source,
                    Value = integrationEvent.Co2eKg,
                    Operator = rule.Operator.ToString(),
                    Threshold = rule.Threshold,
                    Action = rule.Action,
                    TriggeredAt = integrationEvent.OccurredAt,
                    SourceEventId = integrationEvent.EventId,
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
