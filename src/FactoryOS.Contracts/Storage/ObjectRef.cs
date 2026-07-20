namespace FactoryOS.Contracts.Storage;

/// <summary>
/// A lightweight reference to a stored object — its key and metadata without the bytes. Returned when listing a
/// tenant's objects, so callers can enumerate what exists (and how big it is) without loading content.
/// </summary>
public sealed record ObjectRef
{
    /// <summary>The object's key within the tenant.</summary>
    public required string Key { get; init; }

    /// <summary>The MIME content type.</summary>
    public string ContentType { get; init; } = "application/octet-stream";

    /// <summary>The object's size in bytes.</summary>
    public long Size { get; init; }
}
