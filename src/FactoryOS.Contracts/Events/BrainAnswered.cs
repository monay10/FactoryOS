using FactoryOS.Contracts.Ai;

namespace FactoryOS.Contracts.Events;

/// <summary>
/// The shared fact that the Company Brain answered a question, grounded in the tenant's knowledge base. Dashboards,
/// notification and audit consume it to surface the answer and its citations, without referencing the AI layer.
/// The answer re-enters the system as just another event.
/// </summary>
public sealed record BrainAnswered : IntegrationEvent
{
    /// <summary>The tenant the answer is for.</summary>
    public required string Tenant { get; init; }

    /// <summary>The question that was answered.</summary>
    public required string Question { get; init; }

    /// <summary>The generated answer text.</summary>
    public required string Answer { get; init; }

    /// <summary>The upstream chat model that produced the answer.</summary>
    public required string Model { get; init; }

    /// <summary>The knowledge sources the answer was grounded on, in the order presented to the model.</summary>
    public IReadOnlyList<BrainCitation> Citations { get; init; } = [];

    /// <summary>The instant the question was asked (the triggering event's instant).</summary>
    public DateTimeOffset AnsweredAt { get; init; }

    /// <summary>The id of the question event that produced this answer, for idempotent consumers.</summary>
    public Guid SourceEventId { get; init; }
}
