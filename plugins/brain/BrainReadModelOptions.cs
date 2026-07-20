namespace FactoryOS.Plugins.Brain;

/// <summary>
/// Configuration for the Brain read-model module. Behaviour varies by configuration, never by customer branch: a
/// factory sizes how many recent grounded answers it keeps per tenant here.
/// </summary>
public sealed record BrainReadModelOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Modules:Brain";

    /// <summary>The maximum number of recent Q&amp;A entries retained per tenant.</summary>
    public int LogCapacity { get; init; } = 100;
}
