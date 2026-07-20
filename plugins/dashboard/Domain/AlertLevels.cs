namespace FactoryOS.Plugins.Dashboard.Domain;

/// <summary>
/// The normalized urgency labels the board classifies alerts by. The read-model speaks these, not any one
/// module's dialect, so the UI colours a tile the same way whatever raised it.
/// </summary>
public static class AlertLevels
{
    /// <summary>An alert demanding immediate attention (for example a safety stand-down).</summary>
    public const string Critical = "Critical";

    /// <summary>An alert worth surfacing but not halting on (for example a quality or stock breach).</summary>
    public const string Warning = "Warning";

    /// <summary>An informational or positive signal that resolves attention rather than demanding it (for example a
    /// work order being closed). Surfaced on the feed but never a cause for concern.</summary>
    public const string Info = "Info";
}
