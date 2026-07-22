using System.Globalization;

namespace FactoryOS.Plugins.Runtime.Domain;

/// <summary>
/// A published extension point: the platform's side of the contract a plugin plugs into.
/// <para>
/// An extension point is <b>declared by the platform and contributed to by plugins</b>, never the other way
/// round. That direction is the whole reason the architecture holds: an engine publishes what may be
/// extended, and a plugin that wants more has to ask for the point to be published rather than reaching for
/// an internal class.
/// </para>
/// </summary>
/// <param name="Kind">Which point this is.</param>
public readonly record struct PluginExtensionPoint(PluginExtensionPointKind Kind)
{
    /// <summary>Gets the manifest key naming the point (for example <c>humantask</c>).</summary>
    public string Key => PluginExtensionPoints.KeyOf(Kind);

    /// <summary>Gets the permission a plugin must be granted to contribute to this point.</summary>
    public PluginPermission Permission =>
        PluginPermission.Of(Key, PluginPermissions.ExtendAction);

    /// <inheritdoc />
    public override string ToString() => Key;
}

/// <summary>The closed catalogue of extension points the platform publishes.</summary>
public static class PluginExtensionPoints
{
    private static readonly PluginExtensionPointKind[] AllKinds = Enum.GetValues<PluginExtensionPointKind>();

    /// <summary>Gets every published extension point.</summary>
    /// <returns>The points, in declaration order.</returns>
    public static IReadOnlyList<PluginExtensionPoint> All() =>
        [.. AllKinds.Select(kind => new PluginExtensionPoint(kind))];

    /// <summary>Gets the manifest key naming a point.</summary>
    /// <param name="kind">The point.</param>
    /// <returns>The key, lower-cased and without separators.</returns>
    public static string KeyOf(PluginExtensionPointKind kind) => kind switch
    {
        PluginExtensionPointKind.HumanTask => "humantask",
        PluginExtensionPointKind.UiMetadata => "uimetadata",
        _ => kind.ToString().ToLowerInvariant(),
    };

    /// <summary>Attempts to resolve a manifest key to a published extension point.</summary>
    /// <param name="key">The key from a manifest.</param>
    /// <param name="point">The resolved point when the key names one.</param>
    /// <returns><see langword="true"/> when the key names a published point.</returns>
    public static bool TryParse(string? key, out PluginExtensionPoint point)
    {
        point = default;

        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var normalized = key.Trim();
        foreach (var kind in AllKinds)
        {
            if (string.Equals(KeyOf(kind), normalized, StringComparison.OrdinalIgnoreCase))
            {
                point = new PluginExtensionPoint(kind);
                return true;
            }
        }

        return false;
    }

    /// <summary>Resolves a manifest key to a published extension point.</summary>
    /// <param name="key">The key from a manifest.</param>
    /// <returns>The point.</returns>
    /// <exception cref="FormatException">Thrown when the key names no published point.</exception>
    public static PluginExtensionPoint Parse(string key)
    {
        if (!TryParse(key, out var point))
        {
            throw new FormatException(string.Create(
                CultureInfo.InvariantCulture, $"'{key}' is not a published extension point."));
        }

        return point;
    }
}

/// <summary>
/// One thing a plugin contributes to one extension point — a workflow activity, a form field type, a
/// connector definition, a report.
/// <para>
/// A contribution is <b>data</b>: a point, a name and the plugin's own identifier for what it is offering.
/// The runtime never loads or invokes the contribution; it resolves who offers what, and the owning engine
/// asks for the contributions it recognises. That is what keeps the runtime independent of every engine.
/// </para>
/// </summary>
/// <param name="Point">The extension point contributed to.</param>
/// <param name="Name">The contribution's name, unique within the plugin and the point.</param>
public sealed record PluginContribution(PluginExtensionPoint Point, string Name)
{
    /// <summary>Gets an optional human-readable description of what the contribution offers.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the plugin-defined identifier the owning engine resolves the contribution by.</summary>
    public string? Reference { get; init; }

    /// <summary>Builds a contribution to a point.</summary>
    /// <param name="kind">The extension point.</param>
    /// <param name="name">The contribution name.</param>
    /// <returns>The contribution.</returns>
    public static PluginContribution To(PluginExtensionPointKind kind, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new PluginContribution(new PluginExtensionPoint(kind), name.Trim());
    }

    /// <inheritdoc />
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Point.Key}:{Name}");
}

/// <summary>
/// One resolved contribution: what a plugin offers, and which tenant's instance offers it. This is the shape
/// an engine reads when it asks the runtime "who extends me?".
/// </summary>
/// <param name="Tenant">The tenant whose instance contributes it.</param>
/// <param name="PluginKey">The contributing plugin.</param>
/// <param name="Contribution">What is contributed.</param>
public sealed record PluginExtension(string Tenant, string PluginKey, PluginContribution Contribution)
{
    /// <summary>Gets the extension point contributed to.</summary>
    public PluginExtensionPoint Point => Contribution.Point;

    /// <inheritdoc />
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Tenant}|{PluginKey}|{Contribution}");
}
