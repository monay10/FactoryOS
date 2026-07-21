using System.Collections.Concurrent;

namespace FactoryOS.Plugins.Workflow.SLA.Localization;

/// <summary>Resolves localized display text for SLA names, reminder and escalation messages.</summary>
public interface ISlaLocalizer
{
    /// <summary>Resolves the text for a key in a culture, falling back to the supplied default.</summary>
    /// <param name="culture">The requested culture (e.g. <c>en</c>, <c>tr</c>).</param>
    /// <param name="key">The localization key.</param>
    /// <param name="fallback">The text to use when no translation is registered.</param>
    /// <returns>The localized text, or the fallback.</returns>
    string Localize(string culture, string key, string fallback);
}

/// <summary>
/// An in-memory <see cref="ISlaLocalizer"/>. Translations are registered per culture and key; resolution falls
/// back to the supplied default when a culture or key is missing, so an SLA always has text.
/// </summary>
public sealed class InMemorySlaLocalizer : ISlaLocalizer
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _byCulture =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registers a translation.</summary>
    /// <param name="culture">The culture.</param>
    /// <param name="key">The localization key.</param>
    /// <param name="text">The translated text.</param>
    public void Add(string culture, string key, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(text);
        var map = _byCulture.GetOrAdd(culture, _ => new ConcurrentDictionary<string, string>(StringComparer.Ordinal));
        map[key] = text;
    }

    /// <inheritdoc />
    public string Localize(string culture, string key, string fallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(fallback);
        return _byCulture.TryGetValue(culture, out var map) && map.TryGetValue(key, out var text)
            ? text
            : fallback;
    }
}
