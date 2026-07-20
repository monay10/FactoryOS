namespace FactoryOS.Gateway.Modules;

/// <summary>A lightweight, serializable view of a known module for the gateway's inventory endpoint.</summary>
/// <param name="Key">The module's manifest key.</param>
/// <param name="Name">The module's human-readable name.</param>
/// <param name="Version">The module's version, as a display string.</param>
/// <param name="State">The module's current lifecycle state (for example <c>Started</c> or <c>Disabled</c>).</param>
/// <param name="RoutePrefix">The reserved gateway route prefix the module's endpoints are mounted under.</param>
public sealed record ModuleSummary(
    string Key,
    string Name,
    string Version,
    string State,
    string RoutePrefix);
