using System.Collections.Concurrent;

namespace FactoryOS.Plugins.Workflow.Security.Localization;

/// <summary>Resolves localized display text for decisions, violations and incidents.</summary>
public interface ISecurityLocalizer
{
    /// <summary>Resolves the text for a key in a culture, falling back to the supplied default.</summary>
    /// <param name="culture">The requested culture (e.g. <c>en</c>, <c>tr</c>).</param>
    /// <param name="key">The localization key.</param>
    /// <param name="fallback">The text to use when no translation is registered.</param>
    /// <returns>The localized text, or the fallback.</returns>
    string Localize(string culture, string key, string fallback);
}

/// <summary>
/// An in-memory <see cref="ISecurityLocalizer"/>. Translations are registered per culture and key; resolution
/// falls back to the supplied default when a culture or key is missing.
/// <para>
/// Localization applies to how a decision is <b>shown</b>, never to how it is made or recorded. Permission
/// strings, role keys and rule keys stay in the language they were written in — a translated permission would
/// be a different permission, which is a way to lose an authorization check in a language nobody was reading.
/// </para>
/// </summary>
public sealed class InMemorySecurityLocalizer : ISecurityLocalizer
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
