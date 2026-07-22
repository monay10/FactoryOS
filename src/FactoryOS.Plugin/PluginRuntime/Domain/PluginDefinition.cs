using System.Globalization;
using FactoryOS.Contracts.Plugins;

namespace FactoryOS.Plugins.Runtime.Domain;

/// <summary>
/// The platform versions a package supports. Expressed as a floor and an optional ceiling, because a plugin
/// knows what it was built against and cannot know what a future platform will change.
/// </summary>
/// <param name="MinimumPlatform">The lowest platform version the package runs on.</param>
public sealed record PluginCompatibility(PluginVersion MinimumPlatform)
{
    /// <summary>Gets the compatibility that accepts any platform version.</summary>
    public static PluginCompatibility Any { get; } = new(new PluginVersion(0, 0, 0));

    /// <summary>Gets the highest platform version the package is known to run on, if it declares one.</summary>
    public PluginVersion? MaximumPlatform { get; init; }

    /// <summary>Determines whether the package supports a platform version.</summary>
    /// <param name="platform">The running platform version.</param>
    /// <returns><see langword="true"/> when the platform is within the declared range.</returns>
    public bool Supports(PluginVersion platform) =>
        platform >= MinimumPlatform && (MaximumPlatform is not { } max || platform <= max);

    /// <inheritdoc />
    public override string ToString() => MaximumPlatform is { } max
        ? string.Create(CultureInfo.InvariantCulture, $">={MinimumPlatform} <={max}")
        : string.Create(CultureInfo.InvariantCulture, $">={MinimumPlatform}");
}

/// <summary>
/// The runtime's view of one plugin <b>kind</b>: everything that is true of it regardless of which factory
/// runs it. What differs per factory — settings, granted permissions, state — lives on the instance.
/// </summary>
public sealed record PluginDefinition
{
    /// <summary>Gets the stable plugin key.</summary>
    public required string Key { get; init; }

    /// <summary>Gets the human-readable name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the plugin version.</summary>
    public required PluginVersion Version { get; init; }

    /// <summary>Gets an optional description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets an optional author.</summary>
    public string? Author { get; init; }

    /// <summary>Gets the capability keys the plugin provides.</summary>
    public IReadOnlyList<string> Capabilities { get; init; } = [];

    /// <summary>
    /// Gets the capability keys the plugin needs some other plugin to provide.
    /// <para>
    /// Requiring a <i>capability</i> rather than a plugin key is what lets one implementation be swapped for
    /// another: a plugin that needs "a reporting engine" keeps working when the reporting plugin is replaced,
    /// where one that named the plugin would have to be re-released.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> RequiredCapabilities { get; init; } = [];

    /// <summary>Gets the plugins this one depends on.</summary>
    public IReadOnlyList<PluginDependency> Dependencies { get; init; } = [];

    /// <summary>Gets what the plugin contributes, and to which published extension points.</summary>
    public IReadOnlyList<PluginContribution> Contributions { get; init; } = [];

    /// <summary>
    /// Gets the permissions the plugin's manifest asks for. This is a <b>ceiling</b>, not a grant: a tenant
    /// grants a subset of it, and the effective set is the intersection of the two.
    /// </summary>
    public IReadOnlyList<PluginPermission> RequestedPermissions { get; init; } = [];

    /// <summary>Gets how much of the host the plugin is allowed to share.</summary>
    public PluginIsolationMode Isolation { get; init; } = PluginIsolationMode.AssemblyIsolated;

    /// <summary>Gets the platform versions the plugin supports.</summary>
    public PluginCompatibility Compatibility { get; init; } = PluginCompatibility.Any;

    /// <summary>Gets the entry assembly file name, when the plugin is loaded from disk.</summary>
    public string? Assembly { get; init; }

    /// <summary>Gets the full name of the entry type implementing <see cref="IPlugin"/>.</summary>
    public string? EntryType { get; init; }

    /// <summary>Gets the folder the package was read from, when it was read from disk.</summary>
    public string? Location { get; init; }

    /// <summary>Gets the identity a version of a plugin is filed under.</summary>
    public string Identity => Identify(Key, Version);

    /// <summary>Builds the identity a version of a plugin is filed under.</summary>
    /// <param name="key">The plugin key.</param>
    /// <param name="version">The version.</param>
    /// <returns>The identity.</returns>
    public static string Identify(string key, PluginVersion version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return string.Create(CultureInfo.InvariantCulture, $"{key}@{version}");
    }

    /// <summary>
    /// Projects a framework manifest into a runtime definition. Everything the runtime adds — contributions,
    /// requested permissions, isolation and compatibility — is optional, so a manifest written before this
    /// runtime existed still projects cleanly and gets conservative defaults.
    /// </summary>
    /// <param name="manifest">The manifest.</param>
    /// <param name="location">The folder the manifest was read from, if any.</param>
    /// <returns>The definition.</returns>
    public static PluginDefinition FromManifest(PluginManifest manifest, string? location = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        return new PluginDefinition
        {
            Key = manifest.Key,
            Name = manifest.Name,
            Version = manifest.Version,
            Description = manifest.Description,
            Author = manifest.Author,
            Capabilities = manifest.Provides,
            Dependencies = manifest.Dependencies,
            Contributions = ContributionsFrom(manifest),
            Assembly = manifest.Assembly,
            EntryType = manifest.EntryType,
            Location = location,
        };
    }

    /// <summary>Determines whether the plugin provides a capability.</summary>
    /// <param name="capability">The capability key.</param>
    /// <returns><see langword="true"/> when the plugin provides it.</returns>
    public bool Provides(string capability)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capability);
        return Capabilities.Any(provided => string.Equals(provided, capability, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Lists what the plugin contributes to one extension point.</summary>
    /// <param name="kind">The extension point.</param>
    /// <returns>The contributions to that point.</returns>
    public IReadOnlyList<PluginContribution> ContributionsTo(PluginExtensionPointKind kind) =>
        [.. Contributions.Where(contribution => contribution.Point.Kind == kind)];

    /// <summary>
    /// Derives the extension-point permissions the definition implies. A plugin that contributes to the
    /// workflow point necessarily asks for <c>workflow.extend</c>, whether or not its manifest spelled it out
    /// — so a contribution can never smuggle in an ungranted reach into an engine.
    /// </summary>
    /// <returns>The requested permissions, including the implied ones, without duplicates.</returns>
    public IReadOnlyList<PluginPermission> EffectiveRequests()
    {
        var requests = new List<PluginPermission>(RequestedPermissions);

        foreach (var contribution in Contributions)
        {
            var implied = contribution.Point.Permission;
            if (!requests.Contains(implied))
            {
                requests.Add(implied);
            }
        }

        return requests;
    }

    private static IReadOnlyList<PluginContribution> ContributionsFrom(PluginManifest manifest)
    {
        // A manifest's UI screens and API routes are already extension-point contributions; the runtime
        // simply names them as such rather than asking every existing plugin to restate them.
        var contributions = new List<PluginContribution>();

        foreach (var screen in manifest.Ui)
        {
            contributions.Add(PluginContribution.To(PluginExtensionPointKind.UiMetadata, screen.Id) with
            {
                Description = screen.Title,
                Reference = screen.Component,
            });
        }

        foreach (var route in manifest.Api)
        {
            contributions.Add(PluginContribution.To(PluginExtensionPointKind.Api, route.Path) with
            {
                Description = route.Description,
                Reference = route.Method,
            });
        }

        return contributions;
    }
}
