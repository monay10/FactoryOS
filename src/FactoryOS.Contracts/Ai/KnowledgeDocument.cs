namespace FactoryOS.Contracts.Ai;

/// <summary>
/// A raw document to ingest into a tenant's knowledge base. The indexer splits it into chunks, embeds each
/// chunk and stores it for retrieval. Every document is scoped to a <see cref="Tenant"/> — knowledge never
/// crosses tenants.
/// </summary>
public sealed record KnowledgeDocument
{
    /// <summary>The tenant the document belongs to.</summary>
    public required string Tenant { get; init; }

    /// <summary>A stable identifier for the source (for example a file name, URL or record key).</summary>
    public required string Source { get; init; }

    /// <summary>The document's plain-text body.</summary>
    public required string Text { get; init; }
}
