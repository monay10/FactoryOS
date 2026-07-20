namespace FactoryOS.Plugins.Safety.Domain;

/// <summary>The outcome of evaluating an incident: whether to stand down and why.</summary>
/// <param name="StandDown">Whether a stand-down is recommended.</param>
/// <param name="Reason">The reason (<c>HighSeverity</c> or <c>Frequency</c>); empty when no stand-down.</param>
public readonly record struct SafetyDecision(bool StandDown, string Reason)
{
    /// <summary>No stand-down is warranted.</summary>
    public static SafetyDecision None { get; } = new(false, "");
}
