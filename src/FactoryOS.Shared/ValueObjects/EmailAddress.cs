using FactoryOS.Shared.Constants;
using FactoryOS.Shared.Guards;

namespace FactoryOS.Shared.ValueObjects;

/// <summary>
/// A syntactically valid e-mail address, normalized to lowercase. Immutable with value equality. Construction
/// validates the address against <see cref="RegexPatterns.Email"/>.
/// </summary>
public sealed record EmailAddress
{
    private EmailAddress(string value) => Value = value;

    /// <summary>Gets the normalized (lowercase) e-mail address.</summary>
    public string Value { get; }

    /// <summary>Creates an e-mail address, validating and normalizing it.</summary>
    /// <param name="value">The candidate e-mail address.</param>
    /// <returns>A new <see cref="EmailAddress"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the value is not a valid e-mail address.</exception>
    public static EmailAddress Create(string value)
    {
        Guard.AgainstNullOrWhiteSpace(value);
        var trimmed = value.Trim();
        if (!RegexPatterns.EmailMatcher().IsMatch(trimmed))
        {
            throw new ArgumentException($"'{value}' is not a valid e-mail address.", nameof(value));
        }

        return new EmailAddress(trimmed.ToLowerInvariant());
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
