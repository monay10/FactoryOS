using System.Globalization;

namespace FactoryOS.Connectors.Runtime.Domain;

/// <summary>
/// A permission guarding a connector operation, in the platform's <c>resource.action</c> grammar.
/// <para>
/// The grammar is deliberately identical to the one the identity and security layers already speak, so a
/// permission granted there is the same string checked here. The runtime holds a <b>vocabulary</b>, not a
/// second authorization system: it names what may be asked for, and something above it decides.
/// </para>
/// </summary>
/// <param name="Resource">The resource segment (for example <c>connector</c>).</param>
/// <param name="Action">The action segment (for example <c>execute</c>), or <c>*</c> for every action.</param>
public readonly record struct ConnectorPermission(string Resource, string Action)
{
    /// <summary>The wildcard segment matching every value.</summary>
    public const string Wildcard = "*";

    /// <summary>Builds a permission from its two segments.</summary>
    /// <param name="resource">The resource segment.</param>
    /// <param name="action">The action segment.</param>
    /// <returns>The permission.</returns>
    public static ConnectorPermission Of(string resource, string action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        return new ConnectorPermission(resource.Trim(), action.Trim());
    }

    /// <summary>Parses a <c>resource.action</c> string.</summary>
    /// <param name="value">The permission string.</param>
    /// <returns>The permission.</returns>
    /// <exception cref="FormatException">Thrown when the value is not two dot-separated segments.</exception>
    public static ConnectorPermission Parse(string value)
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
    public static bool TryParse(string? value, out ConnectorPermission permission)
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

        permission = new ConnectorPermission(parts[0], parts[1]);
        return true;
    }

    /// <summary>
    /// Determines whether holding this permission covers a requested one. A wildcard segment covers every
    /// value in that position; an exact segment covers only itself.
    /// </summary>
    /// <param name="requested">The permission being asked for.</param>
    /// <returns><see langword="true"/> when this permission grants the requested one.</returns>
    public bool Grants(ConnectorPermission requested) =>
        Covers(Resource, requested.Resource) && Covers(Action, requested.Action);

    /// <inheritdoc />
    public override string ToString() => $"{Resource}.{Action}";

    private static bool Covers(string held, string requested) =>
        string.Equals(held, Wildcard, StringComparison.Ordinal)
        || string.Equals(held, requested, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The permissions the connector runtime recognises. Every operation a definition declares must name one of
/// these, so there is no invocation path that is guarded by nothing.
/// </summary>
public static class ConnectorPermissions
{
    /// <summary>The resource segment every connector permission shares.</summary>
    public const string Resource = "connector";

    /// <summary>Reading data in from an external system.</summary>
    public static readonly ConnectorPermission Read = ConnectorPermission.Of(Resource, "read");

    /// <summary>Writing data out to an external system.</summary>
    public static readonly ConnectorPermission Write = ConnectorPermission.Of(Resource, "write");

    /// <summary>Executing a command against an external system.</summary>
    public static readonly ConnectorPermission Execute = ConnectorPermission.Of(Resource, "execute");

    /// <summary>Starting, stopping and reloading connector instances.</summary>
    public static readonly ConnectorPermission Manage = ConnectorPermission.Of(Resource, "manage");

    /// <summary>Changing a connector instance's configuration or credential reference.</summary>
    public static readonly ConnectorPermission Configure = ConnectorPermission.Of(Resource, "configure");

    /// <summary>Reading connector health, metrics and the catalogue.</summary>
    public static readonly ConnectorPermission Observe = ConnectorPermission.Of(Resource, "observe");

    /// <summary>Gets every permission the runtime recognises.</summary>
    public static IReadOnlyList<ConnectorPermission> Catalogue { get; } =
        [Read, Write, Execute, Manage, Configure, Observe];

    /// <summary>Gets the permission an operation needs, given the capability it exercises.</summary>
    /// <param name="capability">The capability the operation exercises.</param>
    /// <returns>The permission that guards it.</returns>
    public static ConnectorPermission For(Framework.Runtime.ConnectorCapability capability) => capability switch
    {
        Framework.Runtime.ConnectorCapability.Read or Framework.Runtime.ConnectorCapability.Streaming => Read,
        Framework.Runtime.ConnectorCapability.Write or Framework.Runtime.ConnectorCapability.Files
            or Framework.Runtime.ConnectorCapability.Events => Write,
        _ => Execute,
    };
}
