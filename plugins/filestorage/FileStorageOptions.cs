namespace FactoryOS.Plugins.FileStorage;

/// <summary>
/// Configuration for the File Storage module. Behaviour varies by configuration, never by customer branch: a
/// factory caps object size here.
/// </summary>
public sealed record FileStorageOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Modules:FileStorage";

    /// <summary>The maximum object size in bytes a put may store; <c>0</c> means unlimited.</summary>
    public long MaxObjectSizeBytes { get; init; }
}
