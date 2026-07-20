namespace FactoryOS.Contracts.Connectors;

/// <summary>
/// The context a connector reads within. It always carries the tenant, so there is no connector code
/// path that can read outside a single tenant, and optional parameters (for example a since-watermark)
/// supplied by the caller.
/// </summary>
/// <param name="Tenant">The tenant to read data for.</param>
/// <param name="Parameters">Optional read parameters, keyed by name.</param>
public sealed record ConnectorReadContext(string Tenant, IReadOnlyDictionary<string, string>? Parameters = null);
