using System.Globalization;
using FactoryOS.Application.Localization;
using FactoryOS.Infrastructure.Configuration;
using FactoryOS.Shared.Guards;
using FactoryOS.Shared.Identifiers;
using Microsoft.Extensions.Options;

namespace FactoryOS.Infrastructure.Localization;

/// <summary>
/// The default <see cref="ILocalizationService"/>. It resolves a key against an injected catalog (culture → key →
/// template), formats the template with the supplied arguments and falls back to the key itself when no translation
/// exists — the standard key-as-fallback behavior, so a missing translation degrades gracefully rather than failing.
/// </summary>
public sealed class LocalizationProvider : ILocalizationService
{
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _catalog;
    private readonly string _defaultCulture;

    /// <summary>Initializes a new instance of the <see cref="LocalizationProvider"/> class.</summary>
    /// <param name="catalog">The translation catalog, keyed by culture then by localization key.</param>
    /// <param name="options">The infrastructure options carrying the default culture.</param>
    public LocalizationProvider(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> catalog,
        IOptions<InfrastructureOptions> options)
    {
        _catalog = Guard.AgainstNull(catalog);
        Guard.AgainstNull(options);
        _defaultCulture = options.Value.DefaultCulture;
    }

    /// <inheritdoc />
    public string Localize(LocalizationKey key, params object[] arguments) =>
        Localize(key, _defaultCulture, arguments);

    /// <inheritdoc />
    public string Localize(LocalizationKey key, string culture, params object[] arguments)
    {
        Guard.AgainstNullOrWhiteSpace(culture);
        Guard.AgainstNull(arguments);

        var template = Resolve(key.Value, culture) ?? key.Value;
        return arguments.Length == 0
            ? template
            : string.Format(CultureInfo.InvariantCulture, template, arguments);
    }

    private string? Resolve(string key, string culture)
    {
        if (_catalog.TryGetValue(culture, out var entries) && entries.TryGetValue(key, out var template))
        {
            return template;
        }

        if (!string.Equals(culture, _defaultCulture, StringComparison.OrdinalIgnoreCase)
            && _catalog.TryGetValue(_defaultCulture, out var fallback)
            && fallback.TryGetValue(key, out var fallbackTemplate))
        {
            return fallbackTemplate;
        }

        return null;
    }
}
