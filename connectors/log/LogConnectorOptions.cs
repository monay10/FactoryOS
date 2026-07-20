namespace FactoryOS.Connectors.Log;

/// <summary>
/// Configuration for the log transport connector. The transport name it claims is data: a factory can bind this
/// connector to whatever transport its notification routing targets (default <c>log</c>), never a code change.
/// </summary>
public sealed record LogConnectorOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Connectors:Log";

    /// <summary>The transport name this connector delivers for. Dispatches on other transports are ignored.</summary>
    public string Transport { get; init; } = "log";
}
