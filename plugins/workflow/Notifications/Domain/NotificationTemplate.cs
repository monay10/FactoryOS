namespace FactoryOS.Plugins.Workflow.Notifications.Domain;

/// <summary>
/// A channel-specific message template: a subject line (used by channels that have one, such as e-mail) and a
/// body, both written with <c>{{token}}</c> placeholders that the template engine substitutes from the
/// notification's context values. A template is keyed and versioned per channel and culture.
/// </summary>
public sealed class NotificationTemplate
{
    /// <summary>Initializes a new instance of the <see cref="NotificationTemplate"/> class.</summary>
    /// <param name="key">The template key.</param>
    /// <param name="channel">The channel the template renders for.</param>
    /// <param name="body">The body template, with <c>{{token}}</c> placeholders.</param>
    /// <param name="subject">The subject template, for channels that use one.</param>
    /// <param name="culture">The culture the template is written for.</param>
    public NotificationTemplate(
        string key,
        NotificationChannel channel,
        string body,
        string? subject = null,
        string culture = "en")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);
        Key = key;
        Channel = channel;
        Body = body;
        Subject = subject;
        Culture = culture;
    }

    /// <summary>Gets the template key.</summary>
    public string Key { get; }

    /// <summary>Gets the channel the template renders for.</summary>
    public NotificationChannel Channel { get; }

    /// <summary>Gets the subject template, for channels that use one.</summary>
    public string? Subject { get; }

    /// <summary>Gets the body template.</summary>
    public string Body { get; }

    /// <summary>Gets the culture the template is written for.</summary>
    public string Culture { get; }

    /// <summary>The composite registry key for a template: key + channel + culture.</summary>
    /// <param name="key">The template key.</param>
    /// <param name="channel">The channel.</param>
    /// <param name="culture">The culture.</param>
    /// <returns>The composite key.</returns>
    public static string RegistryKey(string key, NotificationChannel channel, string culture) =>
        $"{key}|{channel}|{culture}";

    /// <summary>Gets this template's composite registry key.</summary>
    /// <returns>The composite key.</returns>
    public string ToRegistryKey() => RegistryKey(Key, Channel, Culture);
}

/// <summary>The rendered output of a template: a resolved subject and body.</summary>
/// <param name="Subject">The rendered subject, or <see langword="null"/> when the channel has none.</param>
/// <param name="Body">The rendered body.</param>
public sealed record RenderedMessage(string? Subject, string Body);
