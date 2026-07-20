using FactoryOS.Shared.Constants;
using FactoryOS.Shared.Guards;

namespace FactoryOS.Shared.ValueObjects;

/// <summary>
/// A telephone number in E.164 form. Immutable with value equality. Construction strips spaces, dashes and
/// parentheses, then validates against <see cref="RegexPatterns.E164Phone"/>.
/// </summary>
public sealed record PhoneNumber
{
    private static readonly char[] Separators = [' ', '-', '(', ')', '.'];

    private PhoneNumber(string value) => Value = value;

    /// <summary>Gets the normalized E.164 phone number.</summary>
    public string Value { get; }

    /// <summary>Creates a phone number, normalizing and validating it.</summary>
    /// <param name="value">The candidate phone number.</param>
    /// <returns>A new <see cref="PhoneNumber"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the value is not a valid E.164 phone number.</exception>
    public static PhoneNumber Create(string value)
    {
        Guard.AgainstNullOrWhiteSpace(value);
        var normalized = string.Concat(value.Split(Separators, StringSplitOptions.RemoveEmptyEntries));
        if (!RegexPatterns.PhoneMatcher().IsMatch(normalized))
        {
            throw new ArgumentException($"'{value}' is not a valid E.164 phone number.", nameof(value));
        }

        return new PhoneNumber(normalized);
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
