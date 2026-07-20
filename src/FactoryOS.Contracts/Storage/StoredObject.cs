namespace FactoryOS.Contracts.Storage;

/// <summary>
/// A tenant-scoped binary object — a report, an attachment, an exported file. It is the Standard Model's unit of
/// blob storage: business modules put and get objects through <see cref="IObjectStore"/> without knowing whether
/// the bytes live in MinIO, S3 or memory. Every object carries its <see cref="Tenant"/>; storage never crosses tenants.
/// </summary>
public sealed record StoredObject
{
    /// <summary>The tenant the object belongs to.</summary>
    public required string Tenant { get; init; }

    /// <summary>The object's key within the tenant (a path-like identifier, for example <c>reports/oee/2026-07.pdf</c>).</summary>
    public required string Key { get; init; }

    /// <summary>The MIME content type (for example <c>application/pdf</c> or <c>text/csv</c>).</summary>
    public string ContentType { get; init; } = "application/octet-stream";

    /// <summary>The object's bytes.</summary>
    public required ReadOnlyMemory<byte> Content { get; init; }

    /// <summary>The object's size in bytes.</summary>
    public long Size => Content.Length;
}
