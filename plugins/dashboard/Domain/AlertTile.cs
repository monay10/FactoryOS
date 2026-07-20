namespace FactoryOS.Plugins.Dashboard.Domain;

/// <summary>
/// One entry in the board's live alert feed — a module's alert normalized to what the UI needs: what kind it
/// is, how urgent (<see cref="AlertLevels"/>), a human-readable subject, and when it happened.
/// </summary>
/// <param name="Kind">The originating event type (for example <c>SafetyStandDownTriggered</c>).</param>
/// <param name="Level">The normalized urgency, one of <see cref="AlertLevels"/>.</param>
/// <param name="Subject">A human-readable description of the alert.</param>
/// <param name="OccurredAt">When the alert-triggering event occurred.</param>
public readonly record struct AlertTile(string Kind, string Level, string Subject, DateTimeOffset OccurredAt);
