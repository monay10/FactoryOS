using FactoryOS.Ai.Gateway;
using FactoryOS.Contracts.Events;

namespace FactoryOS.Agents.Insight.Application;

/// <summary>
/// The agent's single reasoning path: turn a normalized <see cref="InsightSignal"/> into an insight by calling
/// the LLM Gateway, then re-enter the result onto the bus as <see cref="InsightGenerated"/>. AI is reached only
/// through the gateway (which speaks HTTP to providers) — never an in-process model call.
/// </summary>
/// <remarks>
/// The trigger is marked processed only after a successful generation, and a gateway failure throws so the bus
/// retries and can eventually dead-letter — an insight is not silently lost. Once emitted, redelivery is a no-op.
/// </remarks>
public sealed class InsightEngine
{
    private readonly IEventBus _bus;
    private readonly ILlmGateway _gateway;
    private readonly IProcessedEventLog _processed;
    private readonly InsightAgentOptions _options;

    /// <summary>Initializes a new instance of the <see cref="InsightEngine"/> class.</summary>
    /// <param name="bus">The event bus to publish insights on.</param>
    /// <param name="gateway">The LLM Gateway — the door to language models.</param>
    /// <param name="processed">The processed-event log for idempotency.</param>
    /// <param name="options">The agent options.</param>
    public InsightEngine(IEventBus bus, ILlmGateway gateway, IProcessedEventLog processed, InsightAgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(processed);
        ArgumentNullException.ThrowIfNull(options);
        _bus = bus;
        _gateway = gateway;
        _processed = processed;
        _options = options;
    }

    /// <summary>Generates and publishes an insight for a signal, exactly once across redeliveries.</summary>
    /// <param name="signal">The normalized trigger.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <exception cref="InvalidOperationException">Thrown when the gateway fails, so the bus can retry.</exception>
    public async Task GenerateAsync(InsightSignal signal, CancellationToken cancellationToken)
    {
        var request = InsightPromptBuilder.Build(signal, _options);
        var result = await _gateway.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"Insight generation failed for {signal.TriggerType}: {result.Error.Code} {result.Error.Description}");
        }

        // Mark only after a successful generation; if another delivery already emitted, this is a no-op.
        if (!_processed.TryMarkProcessed(signal.SourceEventId))
        {
            return;
        }

        var completion = result.Value;
        await _bus.PublishAsync(
            new InsightGenerated
            {
                Tenant = signal.Tenant,
                TriggerType = signal.TriggerType,
                Subject = signal.Subject,
                Insight = completion.Content,
                Model = completion.Model,
                GeneratedAt = signal.OccurredAt,
                SourceEventId = signal.SourceEventId,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
