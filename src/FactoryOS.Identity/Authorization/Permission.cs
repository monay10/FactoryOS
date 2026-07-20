using System.Globalization;
using FactoryOS.Domain.Primitives;

namespace FactoryOS.Identity.Authorization;

/// <summary>
/// A permission expressed as <c>resource.action</c> (e.g. <c>energy.read</c>). Supports wildcards:
/// <c>energy.*</c> grants every action on a resource and <c>*</c> grants everything (super-admin).
/// </summary>
public sealed class Permission : ValueObject
{
    /// <summary>The wildcard token that matches any resource or action.</summary>
    public const string Wildcard = "*";

    private Permission(string value, string resource, string action)
    {
        Value = value;
        Resource = resource;
        Action = action;
    }

    /// <summary>Gets the canonical <c>resource.action</c> string.</summary>
    public string Value { get; }

    /// <summary>Gets the resource segment.</summary>
    public string Resource { get; }

    /// <summary>Gets the action segment.</summary>
    public string Action { get; }

    /// <summary>Parses a permission string.</summary>
    /// <param name="value">The permission string (<c>*</c>, <c>resource.*</c> or <c>resource.action</c>).</param>
    /// <returns>The parsed <see cref="Permission"/>.</returns>
    /// <exception cref="FormatException">Thrown when the string is not a valid permission.</exception>
    public static Permission Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalized = value.Trim().ToLowerInvariant();

        if (normalized == Wildcard)
        {
            return new Permission(Wildcard, Wildcard, Wildcard);
        }

        var parts = normalized.Split('.');
        if (parts.Length != 2 || string.IsNullOrEmpty(parts[0]) || string.IsNullOrEmpty(parts[1]))
        {
            throw new FormatException($"'{value}' is not a valid permission (expected 'resource.action').");
        }

        return new Permission(normalized, parts[0], parts[1]);
    }

    /// <summary>
    /// Determines whether this permission (which may contain wildcards) grants the requested concrete
    /// permission.
    /// </summary>
    /// <param name="requested">The concrete permission being checked.</param>
    /// <returns><see langword="true"/> when this permission covers <paramref name="requested"/>.</returns>
    public bool Grants(Permission requested)
    {
        ArgumentNullException.ThrowIfNull(requested);

        if (Resource == Wildcard)
        {
            return true;
        }

        if (!string.Equals(Resource, requested.Resource, StringComparison.Ordinal))
        {
            return false;
        }

        return Action == Wildcard || string.Equals(Action, requested.Action, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
