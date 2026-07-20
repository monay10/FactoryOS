using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace FactoryOS.Shared.Guards;

/// <summary>
/// Argument guards for defensive programming. Each guard validates a precondition and throws a precise,
/// well-typed exception when it is violated, returning the validated value so it can be assigned inline.
/// Parameter names are captured automatically via <see cref="CallerArgumentExpressionAttribute"/>.
/// </summary>
public static class Guard
{
    /// <summary>Ensures a reference argument is not <see langword="null"/>.</summary>
    /// <typeparam name="T">The argument type.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <param name="name">The captured argument expression (do not pass explicitly).</param>
    /// <returns>The non-null value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    public static T AgainstNull<T>([NotNull] T? value, [CallerArgumentExpression(nameof(value))] string? name = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value, name);
        return value;
    }

    /// <summary>Ensures a string argument is not <see langword="null"/>, empty or whitespace.</summary>
    /// <param name="value">The value to check.</param>
    /// <param name="name">The captured argument expression (do not pass explicitly).</param>
    /// <returns>The validated string.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is null, empty or whitespace.</exception>
    public static string AgainstNullOrWhiteSpace(
        [NotNull] string? value,
        [CallerArgumentExpression(nameof(value))] string? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        return value;
    }

    /// <summary>Ensures a numeric argument is greater than zero.</summary>
    /// <param name="value">The value to check.</param>
    /// <param name="name">The captured argument expression (do not pass explicitly).</param>
    /// <returns>The validated value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> is not positive.</exception>
    public static decimal AgainstNonPositive(
        decimal value,
        [CallerArgumentExpression(nameof(value))] string? name = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, 0m, name);
        return value;
    }

    /// <summary>Ensures a numeric argument is not negative.</summary>
    /// <param name="value">The value to check.</param>
    /// <param name="name">The captured argument expression (do not pass explicitly).</param>
    /// <returns>The validated value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> is negative.</exception>
    public static decimal AgainstNegative(
        decimal value,
        [CallerArgumentExpression(nameof(value))] string? name = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value, name);
        return value;
    }

    /// <summary>Ensures an integer argument falls within an inclusive range.</summary>
    /// <param name="value">The value to check.</param>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound.</param>
    /// <param name="name">The captured argument expression (do not pass explicitly).</param>
    /// <returns>The validated value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> is out of range.</exception>
    public static int AgainstOutOfRange(
        int value,
        int min,
        int max,
        [CallerArgumentExpression(nameof(value))] string? name = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, min, name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, max, name);
        return value;
    }
}
