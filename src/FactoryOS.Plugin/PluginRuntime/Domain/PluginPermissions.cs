using System.Globalization;

namespace FactoryOS.Plugins.Runtime.Domain;

/// <summary>
/// A permission in the platform's <c>resource.action</c> grammar.
/// <para>
/// The grammar is deliberately the same one the identity, security and connector layers already speak, so a
/// permission granted there is the same string checked here. The runtime holds a <b>vocabulary</b>, not a
/// second authorization system: it names what may be asked for, and something above it decides.
/// </para>
/// </summary>
/// <param name="Resource">The resource segment (for example <c>plugin</c>).</param>
/// <param name="Action">The action segment (for example <c>install</c>), or <c>*</c> for every action.</param>
public readonly record struct PluginPermission(string Resource, string Action)
{
    /// <summary>The wildcard segment matching every value.</summary>
    public const string Wildcard = "*";

    /// <summary>Builds a permission from its two segments.</summary>
    /// <param name="resource">The resource segment.</param>
    /// <param name="action">The action segment.</param>
    /// <returns>The permission.</returns>
    public static PluginPermission Of(string resource, string action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        return new PluginPermission(resource.Trim(), action.Trim());
    }

    /// <summary>Parses a <c>resource.action</c> string.</summary>
    /// <param name="value">The permission string.</param>
    /// <returns>The permission.</returns>
    /// <exception cref="FormatException">Thrown when the value is not two dot-separated segments.</exception>
    public static PluginPermission Parse(string value)
    {
        if (!TryParse(value, out var permission))
        {
            throw new FormatException(string.Create(
                CultureInfo.InvariantCulture, $"'{value}' is not a valid permission (expected 'resource.action')."));
        }

        return permission;
    }

    /// <summary>Attempts to parse a <c>resource.action</c> string.</summary>
    /// <param name="value">The permission string.</param>
    /// <param name="permission">The parsed permission when successful.</param>
    /// <returns><see langword="true"/> when the value parsed.</returns>
    public static bool TryParse(string? value, out PluginPermission permission)
    {
        permission = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('.', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
        {
            return false;
        }

        permission = new PluginPermission(parts[0], parts[1]);
        return true;
    }

    /// <summary>
    /// Determines whether holding this permission covers a requested one. A wildcard segment covers every
    /// value in that position; an exact segment covers only itself.
    /// </summary>
    /// <param name="requested">The permission being asked for.</param>
    /// <returns><see langword="true"/> when this permission grants the requested one.</returns>
    public bool Grants(PluginPermission requested) =>
        Covers(Resource, requested.Resource) && Covers(Action, requested.Action);

    /// <inheritdoc />
    public override string ToString() => $"{Resource}.{Action}";

    private static bool Covers(string held, string requested) =>
        string.Equals(held, Wildcard, StringComparison.Ordinal)
        || string.Equals(held, requested, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The permissions the plugin runtime recognises: the administrative ones that guard a lifecycle
/// transition, and the extension-point ones a plugin's manifest requests.
/// </summary>
public static class PluginPermissions
{
    /// <summary>The resource segment the administrative permissions share.</summary>
    public const string Resource = "plugin";

    /// <summary>The action segment an extension-point permission uses.</summary>
    public const string ExtendAction = "extend";

    /// <summary>Placing a package into a tenant's installed set.</summary>
    public static readonly PluginPermission Install = PluginPermission.Of(Resource, "install");

    /// <summary>Loading, unloading, starting and stopping an installed plugin.</summary>
    public static readonly PluginPermission Manage = PluginPermission.Of(Resource, "manage");

    /// <summary>Replacing a plugin's version, and returning to the version replaced.</summary>
    public static readonly PluginPermission Update = PluginPermission.Of(Resource, "update");

    /// <summary>Removing a plugin from a tenant.</summary>
    public static readonly PluginPermission Remove = PluginPermission.Of(Resource, "remove");

    /// <summary>Changing a plugin's settings or its granted permissions.</summary>
    public static readonly PluginPermission Configure = PluginPermission.Of(Resource, "configure");

    /// <summary>Reading the catalogue, health and measurements.</summary>
    public static readonly PluginPermission Observe = PluginPermission.Of(Resource, "observe");

    /// <summary>Gets every administrative permission the runtime recognises.</summary>
    public static IReadOnlyList<PluginPermission> Catalogue { get; } =
        [Install, Manage, Update, Remove, Configure, Observe];

    /// <summary>Gets the permission a lifecycle transition is guarded by.</summary>
    /// <param name="phase">The lifecycle phase being attempted.</param>
    /// <returns>The permission the caller must hold.</returns>
    public static PluginPermission For(PluginLifecyclePhase phase) => phase switch
    {
        PluginLifecyclePhase.Install => Install,
        PluginLifecyclePhase.Update or PluginLifecyclePhase.Rollback => Update,
        PluginLifecyclePhase.Remove => Remove,
        _ => Manage,
    };
}
