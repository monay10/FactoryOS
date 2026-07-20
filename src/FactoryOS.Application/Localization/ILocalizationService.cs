using FactoryOS.Shared.Identifiers;

namespace FactoryOS.Application.Localization;

/// <summary>Resolves localization keys to translated, formatted strings for the caller's culture.</summary>
public interface ILocalizationService
{
    /// <summary>Localizes a key for the ambient culture.</summary>
    /// <param name="key">The localization key.</param>
    /// <param name="arguments">Optional format arguments substituted into the translated string.</param>
    /// <returns>The localized string.</returns>
    string Localize(LocalizationKey key, params object[] arguments);

    /// <summary>Localizes a key for a specific culture.</summary>
    /// <param name="key">The localization key.</param>
    /// <param name="culture">The BCP-47 culture name (for example <c>tr-TR</c>).</param>
    /// <param name="arguments">Optional format arguments substituted into the translated string.</param>
    /// <returns>The localized string.</returns>
    string Localize(LocalizationKey key, string culture, params object[] arguments);
}
