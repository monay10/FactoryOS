using System.Text;
using System.Text.RegularExpressions;
using FactoryOS.Domain.Results;

namespace FactoryOS.Configuration.Secrets;

/// <summary>
/// Expands <c>${secret:NAME}</c> placeholders inside configuration values using an
/// <see cref="ISecretProvider"/>. Unresolved placeholders are reported as a failure so a
/// misconfigured tenant never starts with a missing credential.
/// </summary>
public sealed partial class SecretExpander
{
    private readonly ISecretProvider _secretProvider;

    /// <summary>Initializes a new instance of the <see cref="SecretExpander"/> class.</summary>
    /// <param name="secretProvider">The provider used to resolve secret values.</param>
    public SecretExpander(ISecretProvider secretProvider)
    {
        ArgumentNullException.ThrowIfNull(secretProvider);
        _secretProvider = secretProvider;
    }

    /// <summary>Expands all secret placeholders in a single value.</summary>
    /// <param name="value">The value that may contain <c>${secret:NAME}</c> placeholders.</param>
    /// <returns>A successful result with the expanded value, or a failure naming the missing secret.</returns>
    public Result<string> Expand(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var missing = new List<string>();
        var builder = new StringBuilder(value.Length);
        var lastIndex = 0;

        foreach (Match match in PlaceholderRegex().Matches(value))
        {
            builder.Append(value, lastIndex, match.Index - lastIndex);

            var name = match.Groups["name"].Value;
            if (_secretProvider.TryGet(name, out var resolved) && resolved is not null)
            {
                builder.Append(resolved);
            }
            else
            {
                missing.Add(name);
            }

            lastIndex = match.Index + match.Length;
        }

        builder.Append(value, lastIndex, value.Length - lastIndex);

        if (missing.Count > 0)
        {
            return Result.Failure<string>(
                Error.NotFound("Configuration.Secret.Missing", $"Unresolved secret(s): {string.Join(", ", missing)}."));
        }

        return builder.ToString();
    }

    [GeneratedRegex(@"\$\{secret:(?<name>[^}]+)\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderRegex();
}
