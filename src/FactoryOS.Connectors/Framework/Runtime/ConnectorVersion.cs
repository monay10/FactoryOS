using System.Globalization;

namespace FactoryOS.Connectors.Framework.Runtime;

/// <summary>
/// A three-part semantic connector version (<c>Major.Minor.Patch</c>) with total ordering, so the platform
/// can compare connector versions.
/// </summary>
/// <param name="Major">The major version component.</param>
/// <param name="Minor">The minor version component.</param>
/// <param name="Patch">The patch version component.</param>
public readonly record struct ConnectorVersion(int Major, int Minor, int Patch)
    : IComparable<ConnectorVersion>, IComparable
{
    /// <summary>Parses a <c>Major.Minor.Patch</c> string into a <see cref="ConnectorVersion"/>.</summary>
    /// <param name="value">The version string to parse.</param>
    /// <returns>The parsed <see cref="ConnectorVersion"/>.</returns>
    /// <exception cref="FormatException">Thrown when <paramref name="value"/> is not a valid version.</exception>
    public static ConnectorVersion Parse(string value)
    {
        if (!TryParse(value, out var version))
        {
            throw new FormatException($"'{value}' is not a valid connector version (expected 'Major.Minor.Patch').");
        }

        return version;
    }

    /// <summary>Attempts to parse a <c>Major.Minor.Patch</c> string into a <see cref="ConnectorVersion"/>.</summary>
    /// <param name="value">The version string to parse.</param>
    /// <param name="version">The parsed version when successful; otherwise the default value.</param>
    /// <returns><see langword="true"/> when parsing succeeded; otherwise <see langword="false"/>.</returns>
    public static bool TryParse(string? value, out ConnectorVersion version)
    {
        version = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('.');
        if (parts.Length != 3
            || !TryParseComponent(parts[0], out var major)
            || !TryParseComponent(parts[1], out var minor)
            || !TryParseComponent(parts[2], out var patch))
        {
            return false;
        }

        version = new ConnectorVersion(major, minor, patch);
        return true;
    }

    /// <inheritdoc />
    public int CompareTo(ConnectorVersion other)
    {
        var major = Major.CompareTo(other.Major);
        if (major != 0)
        {
            return major;
        }

        var minor = Minor.CompareTo(other.Minor);
        return minor != 0 ? minor : Patch.CompareTo(other.Patch);
    }

    /// <inheritdoc />
    public int CompareTo(object? obj) => obj switch
    {
        null => 1,
        ConnectorVersion other => CompareTo(other),
        _ => throw new ArgumentException($"Object must be of type {nameof(ConnectorVersion)}.", nameof(obj)),
    };

    /// <summary>Determines whether one version precedes another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is lower than <paramref name="right"/>.</returns>
    public static bool operator <(ConnectorVersion left, ConnectorVersion right) => left.CompareTo(right) < 0;

    /// <summary>Determines whether one version precedes or equals another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is lower than or equal to <paramref name="right"/>.</returns>
    public static bool operator <=(ConnectorVersion left, ConnectorVersion right) => left.CompareTo(right) <= 0;

    /// <summary>Determines whether one version follows another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is higher than <paramref name="right"/>.</returns>
    public static bool operator >(ConnectorVersion left, ConnectorVersion right) => left.CompareTo(right) > 0;

    /// <summary>Determines whether one version follows or equals another.</summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns><see langword="true"/> when <paramref name="left"/> is higher than or equal to <paramref name="right"/>.</returns>
    public static bool operator >=(ConnectorVersion left, ConnectorVersion right) => left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}");

    private static bool TryParseComponent(string text, out int value) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
}
