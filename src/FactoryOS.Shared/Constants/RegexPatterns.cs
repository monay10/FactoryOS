using System.Text.RegularExpressions;

namespace FactoryOS.Shared.Constants;

/// <summary>
/// Shared regular-expression patterns and their compiled, source-generated matchers. The pattern strings are exposed
/// as constants; the matchers are generated at compile time via <see cref="GeneratedRegexAttribute"/> for correctness
/// and performance.
/// </summary>
public static partial class RegexPatterns
{
    /// <summary>A pragmatic e-mail pattern: a local part, an '@', a domain and a dotted TLD, with no whitespace.</summary>
    public const string Email = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

    /// <summary>An E.164 international telephone number: an optional '+' and up to 15 digits.</summary>
    public const string E164Phone = @"^\+?[1-9]\d{1,14}$";

    /// <summary>A URL/identifier slug: lowercase alphanumerics separated by single hyphens.</summary>
    public const string Slug = @"^[a-z0-9]+(?:-[a-z0-9]+)*$";

    /// <summary>Gets the compiled matcher for <see cref="Email"/>.</summary>
    /// <returns>The generated <see cref="Regex"/>.</returns>
    [GeneratedRegex(Email, RegexOptions.CultureInvariant)]
    public static partial Regex EmailMatcher();

    /// <summary>Gets the compiled matcher for <see cref="E164Phone"/>.</summary>
    /// <returns>The generated <see cref="Regex"/>.</returns>
    [GeneratedRegex(E164Phone, RegexOptions.CultureInvariant)]
    public static partial Regex PhoneMatcher();

    /// <summary>Gets the compiled matcher for <see cref="Slug"/>.</summary>
    /// <returns>The generated <see cref="Regex"/>.</returns>
    [GeneratedRegex(Slug, RegexOptions.CultureInvariant)]
    public static partial Regex SlugMatcher();
}
