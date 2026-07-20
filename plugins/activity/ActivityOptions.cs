namespace FactoryOS.Plugins.Activity;

/// <summary>
/// Configuration for the Activity Feed module. Behaviour varies by configuration, never by customer branch: a
/// factory sizes its activity feed here.
/// </summary>
public sealed record ActivityOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Modules:Activity";

    /// <summary>The maximum number of recent activity entries retained per tenant.</summary>
    public int FeedCapacity { get; init; } = 200;
}
