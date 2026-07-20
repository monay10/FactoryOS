namespace FactoryOS.Contracts.Events;

/// <summary>
/// The shared fact that someone (a user, a dashboard, an automated check) asked the Company Brain a question. The
/// Brain Query agent consumes it, answers from the tenant's knowledge base, and re-enters the result on the bus —
/// so asking is decoupled from answering, and any producer can pose a question without referencing the AI layer.
/// The model choice is not carried here; it is the agent's configuration.
/// </summary>
public sealed record BrainQuestionAsked : IntegrationEvent
{
    /// <summary>The tenant asking; scopes retrieval and generation.</summary>
    public required string Tenant { get; init; }

    /// <summary>The natural-language question.</summary>
    public required string Question { get; init; }

    /// <summary>Who or what asked (for example a user id or <c>rule:overtemp-press-1</c>), for traceability.</summary>
    public string AskedBy { get; init; } = string.Empty;

    /// <summary>When the question was asked.</summary>
    public DateTimeOffset AskedAt { get; init; }
}
