namespace FactoryOS.Contracts.Plugins;

/// <summary>
/// The declarative description of a single UI screen a plugin contributes to the shell. Screens are
/// <b>data, not code</b>: the frontend reads the aggregated registry and lazy-loads each screen's
/// component on demand, so the core never references a plugin's UI by name.
/// </summary>
public sealed record PluginUiScreen
{
    /// <summary>Gets the stable, plugin-unique identifier of the screen.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the human-readable screen title shown in navigation.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the client-side route the screen is mounted at (for example <c>/energy/overview</c>).</summary>
    public required string Route { get; init; }

    /// <summary>
    /// Gets the identifier of the lazily loaded frontend component that renders the screen (for
    /// example <c>energy/Overview</c>). The shell resolves it against the module's UI bundle.
    /// </summary>
    public required string Component { get; init; }

    /// <summary>Gets the optional icon key used for the navigation entry.</summary>
    public string? Icon { get; init; }

    /// <summary>Gets the permission a user must hold to see and open the screen, if any.</summary>
    public string? RequiredPermission { get; init; }

    /// <summary>Gets the optional navigation section the screen is grouped under.</summary>
    public string? NavSection { get; init; }

    /// <summary>Gets the sort order of the screen within its navigation section; lower sorts first.</summary>
    public int Order { get; init; }
}
