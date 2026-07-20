namespace FactoryOS.Agents.Knowledge;

/// <summary>
/// The normalized "something happened worth remembering" input the agent ingests. Each specific alert handler
/// maps its event into this one uniform shape — a stable source id plus a plain-text narrative — so the ingest
/// path is single and generic, and the Company Brain can later retrieve and cite it.
/// </summary>
/// <param name="Tenant">The tenant the event belongs to; scopes the knowledge write.</param>
/// <param name="Source">A stable source id for the knowledge document (for example <c>activity/rule/…</c>).</param>
/// <param name="Text">The plain-text narrative to embed and store.</param>
/// <param name="SourceEventId">The producing event's id, for idempotency and traceability.</param>
public readonly record struct KnowledgeSignal(
    string Tenant,
    string Source,
    string Text,
    Guid SourceEventId);
