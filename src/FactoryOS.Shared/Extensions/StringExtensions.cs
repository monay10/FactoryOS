using System.Diagnostics.CodeAnalysis;

namespace FactoryOS.Shared.Extensions;

/// <summary>Convenience extensions for <see cref="string"/>.</summary>
public static class StringExtensions
{
    /// <summary>Determines whether the string is non-null and contains a non-whitespace character.</summary>
    /// <param name="value">The string to test.</param>
    /// <returns><see langword="true"/> when the string has meaningful content.</returns>
    public static bool HasValue([NotNullWhen(true)] this string? value) => !string.IsNullOrWhiteSpace(value);

    /// <summary>Returns <see langword="null"/> when the string is null, empty or whitespace; otherwise the string.</summary>
    /// <param name="value">The string to normalize.</param>
    /// <returns>The string, or <see langword="null"/>.</returns>
    public static string? NullIfWhiteSpace(this string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>Compares two strings for equality ignoring case, using ordinal rules.</summary>
    /// <param name="value">The first string.</param>
    /// <param name="other">The second string.</param>
    /// <returns><see langword="true"/> when the strings are equal ignoring case.</returns>
    public static bool EqualsIgnoreCase(this string? value, string? other) =>
        string.Equals(value, other, StringComparison.OrdinalIgnoreCase);

    /// <summary>Truncates the string to at most a maximum length.</summary>
    /// <param name="value">The string to truncate.</param>
    /// <param name="maxLength">The maximum length; must not be negative.</param>
    /// <returns>The original string, or its first <paramref name="maxLength"/> characters.</returns>
    public static string Truncate(this string value, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfNegative(maxLength);
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
