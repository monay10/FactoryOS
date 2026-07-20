using System.Globalization;
using FactoryOS.Shared.Guards;

namespace FactoryOS.Shared.ValueObjects;

/// <summary>
/// A percentage, stored internally as a ratio where <c>1.0</c> represents 100%. Immutable with value equality.
/// Construct from either a ratio (<see cref="FromRatio"/>) or a percent value (<see cref="FromPercent"/>).
/// </summary>
public sealed record Percentage
{
    private Percentage(decimal ratio) => Ratio = ratio;

    /// <summary>Gets the value as a ratio, where <c>1.0</c> is 100%.</summary>
    public decimal Ratio { get; }

    /// <summary>Gets the value as a percent, where <c>100</c> is 100%.</summary>
    public decimal Percent => Ratio * 100m;

    /// <summary>Zero percent.</summary>
    public static Percentage Zero { get; } = new(0m);

    /// <summary>One hundred percent.</summary>
    public static Percentage OneHundred { get; } = new(1m);

    /// <summary>Creates a percentage from a ratio (where <c>1.0</c> is 100%).</summary>
    /// <param name="ratio">The ratio; must not be negative.</param>
    /// <returns>A new <see cref="Percentage"/>.</returns>
    public static Percentage FromRatio(decimal ratio)
    {
        Guard.AgainstNegative(ratio);
        return new Percentage(ratio);
    }

    /// <summary>Creates a percentage from a percent value (where <c>100</c> is 100%).</summary>
    /// <param name="percent">The percent value; must not be negative.</param>
    /// <returns>A new <see cref="Percentage"/>.</returns>
    public static Percentage FromPercent(decimal percent)
    {
        Guard.AgainstNegative(percent);
        return new Percentage(percent / 100m);
    }

    /// <summary>Applies this percentage to an amount.</summary>
    /// <param name="value">The value to take the percentage of.</param>
    /// <returns>The resulting portion.</returns>
    public decimal Of(decimal value) => value * Ratio;

    /// <inheritdoc />
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Percent:0.##}%");
}
