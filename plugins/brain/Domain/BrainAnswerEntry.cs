using FactoryOS.Contracts.Ai;

namespace FactoryOS.Plugins.Brain.Domain;

/// <summary>
/// One grounded Q&amp;A in a tenant's Brain answer log — the read-model form of a <c>BrainAnswered</c> event.
/// A UI or an operator reads the question, the generated answer, the model that produced it and the knowledge
/// citations it was grounded on, without referencing the AI layer that produced them.
/// </summary>
/// <param name="Tenant">The tenant the answer belongs to.</param>
/// <param name="Question">The question that was asked.</param>
/// <param name="Answer">The generated, grounded answer text.</param>
/// <param name="Model">The upstream chat model that produced the answer.</param>
/// <param name="Citations">The knowledge sources the answer was grounded on, in presentation order.</param>
/// <param name="AnsweredAt">When the question was asked (the triggering event's instant).</param>
/// <param name="SourceEventId">The producing event's id — the log's dedupe key and traceability anchor.</param>
public readonly record struct BrainAnswerEntry(
    string Tenant,
    string Question,
    string Answer,
    string Model,
    IReadOnlyList<BrainCitation> Citations,
    DateTimeOffset AnsweredAt,
    Guid SourceEventId);
