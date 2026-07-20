using FactoryOS.Ai.Brain;
using FactoryOS.Contracts.Ai;
using FactoryOS.Contracts.Events;

namespace FactoryOS.Agents.Brain.Application;

/// <summary>
/// The Brain Query agent's consumer of <see cref="BrainQuestionAsked"/>. It asks the Company Brain — which
/// retrieves grounding from the tenant's knowledge base and generates through the LLM Gateway (both over HTTP,
/// never in-process) — and re-enters the grounded answer on the bus as <see cref="BrainAnswered"/>. It depends
/// only on the <see cref="ICompanyBrain"/> facade and the shared events, never a module.
/// </summary>
/// <remarks>
/// A Brain failure throws so the bus retries and can dead-letter — a question is not silently dropped. The
/// question is marked answered only after success; once answered, redelivery is a no-op.
/// </remarks>
public sealed class BrainQuestionAskedHandler : IEventHandler<BrainQuestionAsked>
{
    private readonly IEventBus _bus;
    private readonly ICompanyBrain _brain;
    private readonly IProcessedEventLog _processed;
    private readonly BrainQueryAgentOptions _options;

    /// <summary>Initializes a new instance of the <see cref="BrainQuestionAskedHandler"/> class.</summary>
    /// <param name="bus">The event bus to publish answers on.</param>
    /// <param name="brain">The Company Brain facade — retrieval + prompting + generation.</param>
    /// <param name="processed">The processed-event log for idempotency.</param>
    /// <param name="options">The agent options carrying the model choices.</param>
    public BrainQuestionAskedHandler(IEventBus bus, ICompanyBrain brain, IProcessedEventLog processed, BrainQueryAgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(brain);
        ArgumentNullException.ThrowIfNull(processed);
        ArgumentNullException.ThrowIfNull(options);
        _bus = bus;
        _brain = brain;
        _processed = processed;
        _options = options;
    }

    /// <inheritdoc />
    public async Task HandleAsync(BrainQuestionAsked integrationEvent, EventContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);

        var question = new BrainQuestion
        {
            Tenant = integrationEvent.Tenant,
            Question = integrationEvent.Question,
            ChatModel = _options.ChatModel,
            EmbeddingModel = _options.EmbeddingModel,
            TopK = _options.TopK,
        };

        var result = await _brain.AskAsync(question, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"Brain query failed for tenant {integrationEvent.Tenant}: {result.Error.Code} {result.Error.Description}");
        }

        // Mark only after a successful answer; if another delivery already answered, this is a no-op.
        if (!_processed.TryMarkProcessed(integrationEvent.EventId))
        {
            return;
        }

        var answer = result.Value;
        await _bus.PublishAsync(
            new BrainAnswered
            {
                Tenant = integrationEvent.Tenant,
                Question = integrationEvent.Question,
                Answer = answer.Answer,
                Model = answer.Model,
                Citations = answer.Citations,
                AnsweredAt = integrationEvent.AskedAt,
                SourceEventId = integrationEvent.EventId,
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
